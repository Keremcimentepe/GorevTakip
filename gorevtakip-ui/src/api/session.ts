// Giriş yapmış kullanıcının oturumu (JWT + e-posta + bitiş zamanı).
// localStorage'da saklanır; sayfa yenilenince ya da sekme kapanıp açılınca oturum sürer.
//
// Güvenlik notu: localStorage'daki token, sayfaya bir XSS açığıyla kod sokan birine açıktır.
// Bu yüzden arayüzde kullanıcı verisini HTML olarak basmayız (React metni otomatik kaçışlar).
// Daha sıkı bir alternatif, token'ı sunucunun koyduğu HttpOnly çerezde tutmaktır; bu proje için
// bilinçli olarak daha basit olan yolu seçtik.

import type { AuthResponse } from '../types'

export type Session = AuthResponse

const STORAGE_KEY = 'gorevtakip.session'

// localStorage çalışmazsa (bazı gizli sekmeler, engellenmiş site verisi) oturum sayfa açık kaldığı
// sürece burada, bellekte durur: uygulama bozulmaz, sadece yenileyince çıkış yapılmış olur.
let memoryRaw: string | null = null

// React'in useSyncExternalStore'u, aynı veri için HER çağrıda aynı nesneyi bekler.
// Ham metin değişmedikçe önceki ayrıştırma sonucunu veririz.
let cachedRaw: string | null = null
let cachedSession: Session | null = null

const listeners = new Set<() => void>()

function readRaw(): string | null {
  try {
    return localStorage.getItem(STORAGE_KEY)
  } catch {
    return memoryRaw
  }
}

function parse(raw: string | null): Session | null {
  if (!raw) return null
  try {
    const data: unknown = JSON.parse(raw)
    if (typeof data !== 'object' || data === null) return null
    const { token, email, expiresAt } = data as Record<string, unknown>
    if (typeof token !== 'string' || token === '') return null
    if (typeof email !== 'string' || typeof expiresAt !== 'string') return null
    if (Number.isNaN(Date.parse(expiresAt))) return null
    return { token, email, expiresAt }
  } catch {
    return null // bozuk kayıt: oturum yokmuş gibi davran
  }
}

function notify() {
  listeners.forEach((listener) => listener())
}

/** Geçerli oturum; yoksa ya da süresi dolmuşsa null. */
export function getSession(): Session | null {
  const raw = readRaw()
  if (raw !== cachedRaw) {
    cachedRaw = raw
    cachedSession = parse(raw)
  }
  if (cachedSession && Date.parse(cachedSession.expiresAt) <= Date.now()) return null
  return cachedSession
}

export function getToken(): string | null {
  return getSession()?.token ?? null
}

export function saveSession(session: Session) {
  const raw = JSON.stringify({ token: session.token, email: session.email, expiresAt: session.expiresAt })
  memoryRaw = raw
  try {
    localStorage.setItem(STORAGE_KEY, raw)
  } catch {
    // Depolama doluysa/engelliyse bellekteki kopya yeter
  }
  notify()
}

export function clearSession() {
  memoryRaw = null
  try {
    localStorage.removeItem(STORAGE_KEY)
  } catch {
    // yoksay
  }
  notify()
}

/** Oturum değişince çağrılır (giriş, çıkış, başka sekmede çıkış). Dönen fonksiyon aboneliği bitirir. */
export function subscribeSession(listener: () => void): () => void {
  listeners.add(listener)
  return () => {
    listeners.delete(listener)
  }
}

// Başka bir sekmede çıkış/giriş yapılırsa bu sekme de haberdar olsun.
// ("storage" olayı sadece DİĞER sekmelerde tetiklenir; key === null → localStorage.clear())
if (typeof window !== 'undefined') {
  window.addEventListener('storage', (event) => {
    if (event.key === null || event.key === STORAGE_KEY) notify()
  })
}
