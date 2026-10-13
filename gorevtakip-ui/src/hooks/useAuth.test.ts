import { act, renderHook } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { login, register } from '../api/auth'
import { ApiError, get } from '../api/client'
import { clearSession, getSession, saveSession } from '../api/session'
import { SESSION_EXPIRED_NOTICE, useAuth } from './useAuth'

vi.mock('../api/auth')

const AUTH = { token: 'jwt', expiresAt: '2099-01-01T00:00:00Z', email: 'ali@ornek.com' }

beforeEach(() => {
  vi.resetAllMocks()
})

afterEach(() => {
  clearSession()
  vi.unstubAllGlobals()
})

// Sunucunun token'ı reddetmesini taklit eder: korumalı bir istek 401 alır.
// (Gerçek client.ts çalışır; oturumu silip hook'u haberdar eden de o.)
async function serverRejectsTheToken() {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(null, { status: 401 })))
  await act(async () => {
    await get('/projects').catch(() => {})
  })
}

describe('useAuth: başlangıç', () => {
  it('kayıtlı oturum yoksa session null döner', () => {
    const { result } = renderHook(() => useAuth())

    expect(result.current.session).toBeNull()
    expect(result.current.notice).toBeNull()
  })

  it('localStorage\'da geçerli oturum varsa onunla başlar (sayfa yenileme)', () => {
    saveSession(AUTH)

    const { result } = renderHook(() => useAuth())

    expect(result.current.session).toEqual(AUTH)
  })
})

describe('useAuth: giriş ve kayıt', () => {
  it('login başarılıysa oturumu kaydeder ve null (hata yok) döner', async () => {
    vi.mocked(login).mockResolvedValue(AUTH)
    const { result } = renderHook(() => useAuth())

    let message: string | null = 'dokunulmadı'
    await act(async () => {
      message = await result.current.login('ali@ornek.com', 'Sifre1234!')
    })

    expect(message).toBeNull()
    expect(login).toHaveBeenCalledWith('ali@ornek.com', 'Sifre1234!')
    expect(result.current.session).toEqual(AUTH)
    expect(getSession()).toEqual(AUTH)
  })

  it('login başarısızsa hata mesajını döner ve oturum açmaz', async () => {
    vi.mocked(login).mockRejectedValue(new ApiError('E-posta veya şifre hatalı.', 401))
    const { result } = renderHook(() => useAuth())

    let message: string | null = null
    await act(async () => {
      message = await result.current.login('ali@ornek.com', 'yanlis')
    })

    expect(message).toBe('E-posta veya şifre hatalı.')
    expect(result.current.session).toBeNull()
    expect(getSession()).toBeNull()
  })

  it('register başarılıysa doğrudan oturum açar', async () => {
    vi.mocked(register).mockResolvedValue(AUTH)
    const { result } = renderHook(() => useAuth())

    await act(async () => {
      await result.current.register('ali@ornek.com', 'Sifre1234!')
    })

    expect(register).toHaveBeenCalledWith('ali@ornek.com', 'Sifre1234!')
    expect(result.current.session).toEqual(AUTH)
  })

  it('register başarısızsa (ör. e-posta kayıtlı) mesajı döner', async () => {
    vi.mocked(register).mockRejectedValue(new ApiError('Bu e-posta adresiyle zaten bir hesap var.', 409))
    const { result } = renderHook(() => useAuth())

    let message: string | null = null
    await act(async () => {
      message = await result.current.register('ali@ornek.com', 'Sifre1234!')
    })

    expect(message).toBe('Bu e-posta adresiyle zaten bir hesap var.')
    expect(result.current.session).toBeNull()
  })
})

describe('useAuth: çıkış ve oturum bitişi', () => {
  it('logout oturumu siler', () => {
    saveSession(AUTH)
    const { result } = renderHook(() => useAuth())

    act(() => result.current.logout())

    expect(result.current.session).toBeNull()
    expect(getSession()).toBeNull()
  })

  it('başka sekmede çıkış yapılırsa (oturum dışarıdan silinirse) burada da çıkış olur', () => {
    saveSession(AUTH)
    const { result } = renderHook(() => useAuth())

    act(() => {
      localStorage.removeItem('gorevtakip.session')
      window.dispatchEvent(new StorageEvent('storage', { key: 'gorevtakip.session' }))
    })

    expect(result.current.session).toBeNull()
  })

  it('sunucu token\'ı reddedince (401) oturum biter ve "süre doldu" bilgisi çıkar', async () => {
    saveSession(AUTH)
    const { result } = renderHook(() => useAuth())

    await serverRejectsTheToken()

    expect(result.current.session).toBeNull()
    expect(result.current.notice).toBe(SESSION_EXPIRED_NOTICE)
  })

  it('başarılı girişten sonra "süre doldu" bilgisi kalkar', async () => {
    saveSession(AUTH)
    vi.mocked(login).mockResolvedValue(AUTH)
    const { result } = renderHook(() => useAuth())
    await serverRejectsTheToken()
    expect(result.current.notice).toBe(SESSION_EXPIRED_NOTICE)

    await act(async () => {
      await result.current.login('ali@ornek.com', 'Sifre1234!')
    })

    expect(result.current.notice).toBeNull()
  })

  it('başarısız girişte "süre doldu" bilgisi kalır', async () => {
    saveSession(AUTH)
    vi.mocked(login).mockRejectedValue(new ApiError('E-posta veya şifre hatalı.', 401))
    const { result } = renderHook(() => useAuth())
    await serverRejectsTheToken()

    await act(async () => {
      await result.current.login('ali@ornek.com', 'yanlis')
    })

    expect(result.current.notice).toBe(SESSION_EXPIRED_NOTICE)
  })

  it('kullanıcı kendi çıkış yapınca "süre doldu" bilgisi gösterilmez', async () => {
    saveSession(AUTH)
    const { result } = renderHook(() => useAuth())
    await serverRejectsTheToken()
    expect(result.current.notice).toBe(SESSION_EXPIRED_NOTICE)

    act(() => result.current.logout())

    expect(result.current.notice).toBeNull()
  })
})
