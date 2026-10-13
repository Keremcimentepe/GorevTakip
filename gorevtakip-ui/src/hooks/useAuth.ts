import { useEffect, useState, useSyncExternalStore } from 'react'
import { login, register } from '../api/auth'
import { getErrorMessage, setUnauthorizedHandler } from '../api/client'
import { clearSession, getSession, saveSession, subscribeSession } from '../api/session'

export const SESSION_EXPIRED_NOTICE = 'Oturumun süresi doldu. Devam etmek için yeniden giriş yap.'

// Giriş durumunu tek yerde toplar. "Kimin girdiği" localStorage'daki oturumdan gelir
// (useSyncExternalStore: oturum değişince, başka sekmede olsa bile, bileşen yeniden çizilir).
export function useAuth() {
  const session = useSyncExternalStore(subscribeSession, getSession)
  const [notice, setNotice] = useState<string | null>(null)

  // Sunucu token'ı reddederse (süre dolmuş / geçersiz) client.ts oturumu siler ve bizi çağırır.
  useEffect(() => setUnauthorizedHandler(() => setNotice(SESSION_EXPIRED_NOTICE)), [])

  // Başarılıysa null, başarısızsa kullanıcıya gösterilecek hata mesajını döner.
  async function authenticate(action: typeof login, email: string, password: string): Promise<string | null> {
    try {
      saveSession(await action(email, password))
      setNotice(null)
      return null
    } catch (e) {
      return getErrorMessage(e)
    }
  }

  return {
    session,
    notice,
    login: (email: string, password: string) => authenticate(login, email, password),
    register: (email: string, password: string) => authenticate(register, email, password),
    logout: () => {
      setNotice(null)
      clearSession()
    },
  }
}
