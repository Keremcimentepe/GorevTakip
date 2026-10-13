import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError, del, get, getErrorMessage, patch, post, setUnauthorizedHandler } from './client'
import { clearSession, getSession, saveSession } from './session'

// Gerçek ağa çıkmıyoruz: fetch'i sahtesiyle değiştirip
// "sunucu şunu cevaplarsa istemci ne yapar?" sorusunu sınıyoruz.
const fetchMock = vi.fn()

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

// İsteğin ApiError ile başarısız olmasını bekler ve hatayı döner
async function catchApiError(promise: Promise<unknown>): Promise<ApiError> {
  try {
    await promise
  } catch (error) {
    if (error instanceof ApiError) return error
    throw error
  }
  throw new Error('İstek başarısız olmalıydı ama başarılı oldu.')
}

beforeEach(() => {
  fetchMock.mockReset()
  vi.stubGlobal('fetch', fetchMock)
})

afterEach(() => {
  vi.unstubAllGlobals()
  vi.unstubAllEnvs()
  clearSession()
  setUnauthorizedHandler(null)
})

describe('istek gönderme', () => {
  it('get: JSON cevabı döner', async () => {
    fetchMock.mockResolvedValue(jsonResponse([{ id: 1 }]))

    await expect(get('/projects')).resolves.toEqual([{ id: 1 }])
  })

  it('get: adrese yolu ekler ve JSON başlığı gönderir', async () => {
    fetchMock.mockResolvedValue(jsonResponse({}))

    await get('/projects')

    const [url, options] = fetchMock.mock.calls[0]
    expect(String(url)).toMatch(/\/projects$/)
    expect(options.headers).toMatchObject({ 'Content-Type': 'application/json' })
  })

  it('post: POST metodu ve JSON gövdesi gönderir', async () => {
    fetchMock.mockResolvedValue(jsonResponse({ id: 5 }, 201))

    const result = await post('/projects', { name: 'Web' })

    const [, options] = fetchMock.mock.calls[0]
    expect(options.method).toBe('POST')
    expect(options.body).toBe('{"name":"Web"}')
    expect(result).toEqual({ id: 5 })
  })

  it('patch: PATCH metodu ve JSON gövdesi gönderir', async () => {
    fetchMock.mockResolvedValue(jsonResponse({ id: 5 }))

    await patch('/tasks/5/status', { status: 'Done' })

    const [, options] = fetchMock.mock.calls[0]
    expect(options.method).toBe('PATCH')
    expect(options.body).toBe('{"status":"Done"}')
  })

  it('del: DELETE gönderir; 204 (gövdesiz) cevapta undefined döner', async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 204 }))

    await expect(del('/tasks/5')).resolves.toBeUndefined()

    const [, options] = fetchMock.mock.calls[0]
    expect(options.method).toBe('DELETE')
  })

  it('adresi VITE_API_URL ortam değişkeninden alır', async () => {
    vi.stubEnv('VITE_API_URL', 'http://api.test:9000')
    vi.resetModules() // BASE_URL modül yüklenirken okunur; modülü baştan yüklemek gerekir
    const freshClient = await import('./client')
    fetchMock.mockResolvedValue(jsonResponse({}))

    await freshClient.get('/projects')

    expect(fetchMock.mock.calls[0][0]).toBe('http://api.test:9000/projects')
  })
})

describe('hata cevapları', () => {
  it('400: alan hatalarını tek bir mesajda birleştirir', async () => {
    fetchMock.mockResolvedValue(
      jsonResponse(
        {
          errors: {
            Title: ['Görev başlığı boş olamaz.'],
            Description: ['Açıklama en fazla 1000 karakter olabilir.'],
          },
        },
        400,
      ),
    )

    const error = await catchApiError(post('/projects/1/tasks', {}))

    expect(error.message).toBe('Görev başlığı boş olamaz. Açıklama en fazla 1000 karakter olabilir.')
    expect(error.status).toBe(400)
  })

  it('500: ProblemDetails başlığını mesaj olarak kullanır', async () => {
    fetchMock.mockResolvedValue(jsonResponse({ status: 500, title: 'Beklenmeyen bir hata oluştu.' }, 500))

    const error = await catchApiError(get('/projects'))

    expect(error.message).toBe('Beklenmeyen bir hata oluştu.')
    expect(error.status).toBe(500)
  })

  it('404: gövde yoksa "Kayıt bulunamadı." der', async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 404 }))

    const error = await catchApiError(get('/projects/99'))

    expect(error.message).toBe('Kayıt bulunamadı.')
    expect(error.status).toBe(404)
  })

  it('JSON olmayan hata gövdesinde durum koduyla genel bir mesaj verir', async () => {
    fetchMock.mockResolvedValue(new Response('Service Unavailable', { status: 503 }))

    const error = await catchApiError(get('/projects'))

    expect(error.message).toBe('Sunucu hatası (503).')
  })

  it('JSON var ama tanıdık alan yoksa genel mesaja düşer', async () => {
    fetchMock.mockResolvedValue(jsonResponse({ something: 'else' }, 500))

    const error = await catchApiError(get('/projects'))

    expect(error.message).toBe('Sunucu hatası (500).')
  })

  it('sunucuya ulaşılamazsa durum kodu 0 olan anlaşılır bir hata verir', async () => {
    fetchMock.mockRejectedValue(new TypeError('Failed to fetch'))

    const error = await catchApiError(get('/projects'))

    expect(error.status).toBe(0)
    expect(error.message).toContain("API'ye ulaşılamadı")
  })
})

function signIn(token = 'jwt-1') {
  saveSession({ token, email: 'ali@ornek.com', expiresAt: '2099-01-01T00:00:00Z' })
}

describe('oturum (token)', () => {
  it('oturum varsa her isteğe Authorization: Bearer başlığını ekler', async () => {
    signIn('jwt-1')
    // Her çağrıya yeni bir Response: aynı nesnenin gövdesi ikinci kez okunamaz
    fetchMock.mockImplementation(async () => jsonResponse({}))

    await get('/projects')
    await post('/projects', { name: 'Web' })
    await patch('/tasks/1/status', { status: 'Done' })
    await del('/tasks/1')

    for (const [, options] of fetchMock.mock.calls) {
      expect(options.headers).toMatchObject({ Authorization: 'Bearer jwt-1', 'Content-Type': 'application/json' })
    }
  })

  it('oturum yoksa Authorization başlığı eklemez', async () => {
    fetchMock.mockResolvedValue(jsonResponse({}))

    await get('/projects')

    expect(fetchMock.mock.calls[0][1].headers).not.toHaveProperty('Authorization')
  })

  it('auth: false olan istekte oturum olsa bile token göndermez', async () => {
    signIn()
    fetchMock.mockResolvedValue(jsonResponse({}))

    await post('/auth/login', { email: 'a@b.c', password: 'x' }, { auth: false })

    expect(fetchMock.mock.calls[0][1].headers).not.toHaveProperty('Authorization')
  })

  it('süresi dolmuş token gönderilmez', async () => {
    saveSession({ token: 'eski', email: 'ali@ornek.com', expiresAt: '2000-01-01T00:00:00Z' })
    fetchMock.mockResolvedValue(jsonResponse({}))

    await get('/projects')

    expect(fetchMock.mock.calls[0][1].headers).not.toHaveProperty('Authorization')
  })

  it('401: oturumu siler, dinleyiciyi çağırır ve anlaşılır bir hata verir', async () => {
    signIn()
    const handler = vi.fn()
    setUnauthorizedHandler(handler)
    fetchMock.mockResolvedValue(new Response(null, { status: 401 }))

    const error = await catchApiError(get('/projects'))

    expect(error.status).toBe(401)
    expect(error.message).toBe('Oturumun süresi dolmuş ya da geçersiz. Lütfen yeniden giriş yap.')
    expect(getSession()).toBeNull()
    expect(handler).toHaveBeenCalledTimes(1)
  })

  it('401 (giriş denemesi): sunucunun mesajını verir, kayıtlı oturuma ve dinleyiciye dokunmaz', async () => {
    signIn()
    const handler = vi.fn()
    setUnauthorizedHandler(handler)
    fetchMock.mockResolvedValue(jsonResponse({ status: 401, title: 'E-posta veya şifre hatalı.' }, 401))

    const error = await catchApiError(post('/auth/login', { email: 'a@b.c', password: 'x' }, { auth: false }))

    expect(error.message).toBe('E-posta veya şifre hatalı.')
    expect(getSession()).not.toBeNull()
    expect(handler).not.toHaveBeenCalled()
  })

  it('401 ama zaten oturum yoksa dinleyiciyi çağırmaz (çıkış yapmış kullanıcıya "süre doldu" denmez)', async () => {
    const handler = vi.fn()
    setUnauthorizedHandler(handler)
    fetchMock.mockResolvedValue(new Response(null, { status: 401 }))

    await catchApiError(get('/projects'))

    expect(handler).not.toHaveBeenCalled()
  })

  it('401 gelene kadar kullanıcı yeniden giriş yaptıysa yeni oturum silinmez', async () => {
    signIn('eski-token')
    const handler = vi.fn()
    setUnauthorizedHandler(handler)
    fetchMock.mockImplementation(async () => {
      signIn('yeni-token') // istek yoldayken kullanıcı başka hesapla girdi
      return new Response(null, { status: 401 })
    })

    await catchApiError(get('/projects'))

    expect(getSession()?.token).toBe('yeni-token')
    expect(handler).not.toHaveBeenCalled()
  })

  it('dinleyicinin aboneliği bitince çağrılmaz', async () => {
    signIn()
    const handler = vi.fn()
    const unsubscribe = setUnauthorizedHandler(handler)
    unsubscribe()
    fetchMock.mockResolvedValue(new Response(null, { status: 401 }))

    await catchApiError(get('/projects'))

    expect(handler).not.toHaveBeenCalled()
  })

  it('429: "çok fazla deneme" mesajını verir', async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 429 }))

    const error = await catchApiError(post('/auth/login', {}, { auth: false }))

    expect(error.status).toBe(429)
    expect(error.message).toBe('Çok fazla deneme yaptın. Biraz bekleyip tekrar dene.')
  })
})

describe('ApiError ve getErrorMessage', () => {
  it('ApiError hem Error hem ApiError olarak tanınır', () => {
    const error = new ApiError('Bir şey oldu', 418)

    expect(error).toBeInstanceOf(Error)
    expect(error).toBeInstanceOf(ApiError)
    expect(error.name).toBe('ApiError')
    expect(error.status).toBe(418)
  })

  it('getErrorMessage: Error ise mesajını verir', () => {
    expect(getErrorMessage(new Error('Merhaba'))).toBe('Merhaba')
  })

  it('getErrorMessage: Error değilse varsayılan mesajı verir', () => {
    expect(getErrorMessage('metin')).toBe('Bilinmeyen bir hata oluştu.')
    expect(getErrorMessage(undefined)).toBe('Bilinmeyen bir hata oluştu.')
  })
})
