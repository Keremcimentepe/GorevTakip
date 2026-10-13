// Tüm HTTP istekleri buradan geçer. Bileşenler fetch'i doğrudan bilmez;
// adres, JSON dönüşümü, giriş token'ı ve hata mesajı çıkarma işi tek yerde durur.

import { clearSession, getToken } from './session'

const BASE_URL: string = import.meta.env.VITE_API_URL ?? 'http://localhost:5291'

export class ApiError extends Error {
  status: number

  constructor(message: string, status: number) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

// Backend'in döndürdüğü hata gövdesinden okunur bir mesaj çıkarır.
// 400: { errors: { title: ["Görev başlığı boş olamaz."] } }
// 500: { title: "Beklenmeyen bir hata oluştu." }
async function readErrorMessage(response: Response): Promise<string> {
  try {
    const data = await response.json()
    if (data.errors) {
      return Object.values(data.errors as Record<string, string[]>).flat().join(' ')
    }
    if (data.title) {
      return data.title
    }
  } catch {
    // Gövde JSON değilse aşağıdaki varsayılan mesajlara düş
  }
  if (response.status === 401) return 'Oturumun süresi dolmuş ya da geçersiz. Lütfen yeniden giriş yap.'
  if (response.status === 404) return 'Kayıt bulunamadı.'
  if (response.status === 429) return 'Çok fazla deneme yaptın. Biraz bekleyip tekrar dene.'
  return `Sunucu hatası (${response.status}).`
}

// Oturum sunucu tarafından reddedilince (token süresi dolmuş ya da geçersiz) haber verilecek yer.
// Arayüz buraya "giriş ekranına dön ve nedenini söyle" işini bağlar.
let unauthorizedHandler: (() => void) | null = null

export function setUnauthorizedHandler(handler: (() => void) | null): () => void {
  unauthorizedHandler = handler
  return () => {
    if (unauthorizedHandler === handler) unauthorizedHandler = null
  }
}

interface RequestOptions extends RequestInit {
  /** false: Authorization başlığı eklenmez (giriş/kayıt isteklerinde). Varsayılan true. */
  auth?: boolean
}

async function request<T>(path: string, { auth = true, ...options }: RequestOptions = {}): Promise<T> {
  const token = auth ? getToken() : null
  const headers: Record<string, string> = { 'Content-Type': 'application/json' }
  if (token) headers.Authorization = `Bearer ${token}`

  let response: Response
  try {
    response = await fetch(`${BASE_URL}${path}`, {
      ...options,
      headers: { ...headers, ...(options.headers as Record<string, string> | undefined) },
    })
  } catch {
    // fetch, sunucuya hiç ulaşamazsa (kapalı, yanlış port, CORS) hata fırlatır
    throw new ApiError('API\'ye ulaşılamadı. Sunucu çalışıyor mu, adres doğru mu?', 0)
  }

  if (!response.ok) {
    // Token gönderdik ve reddedildi: oturum bitmiş demektir. Yanlış şifreli giriş denemesi (auth: false)
    // buraya girmez. İstek sürerken kullanıcı yeniden giriş yaptıysa (token değiştiyse) o yeni oturumu
    // eski isteğin 401'i yüzünden silmeyiz.
    if (response.status === 401 && token && getToken() === token) {
      clearSession()
      unauthorizedHandler?.()
    }
    throw new ApiError(await readErrorMessage(response), response.status)
  }

  // 204 No Content: gövde yok, json() çağırırsak hata verir
  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}

export function get<T>(path: string) {
  return request<T>(path)
}

export function post<T>(path: string, body: unknown, options: { auth?: boolean } = {}) {
  return request<T>(path, { ...options, method: 'POST', body: JSON.stringify(body) })
}

export function patch<T>(path: string, body: unknown) {
  return request<T>(path, { method: 'PATCH', body: JSON.stringify(body) })
}

export function del(path: string) {
  return request<void>(path, { method: 'DELETE' })
}

export function getErrorMessage(error: unknown): string {
  return error instanceof Error ? error.message : 'Bilinmeyen bir hata oluştu.'
}
