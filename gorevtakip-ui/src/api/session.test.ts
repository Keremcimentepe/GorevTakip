import { afterEach, describe, expect, it, vi } from 'vitest'
import { clearSession, getSession, getToken, saveSession, subscribeSession } from './session'

const KEY = 'gorevtakip.session'

// Şimdiden 1 saat sonrası: süresi dolmamış oturum
function futureDate() {
  return new Date(Date.now() + 60 * 60 * 1000).toISOString()
}

function makeSession(overrides: Partial<{ token: string; email: string; expiresAt: string }> = {}) {
  return { token: 'abc.def.ghi', email: 'ali@ornek.com', expiresAt: futureDate(), ...overrides }
}

afterEach(() => {
  clearSession() // bellekteki yedeği de sıfırla
  vi.useRealTimers()
  vi.restoreAllMocks()
})

describe('oturum saklama', () => {
  it('kayıt yoksa oturum yoktur', () => {
    expect(getSession()).toBeNull()
    expect(getToken()).toBeNull()
  })

  it('kaydedilen oturumu geri verir ve localStorage\'a yazar', () => {
    const session = makeSession()

    saveSession(session)

    expect(getSession()).toEqual(session)
    expect(getToken()).toBe('abc.def.ghi')
    expect(JSON.parse(localStorage.getItem(KEY)!)).toEqual(session)
  })

  it('sayfa yenilenince (localStorage\'da kalan kayıttan) oturum geri gelir', () => {
    const session = makeSession()
    localStorage.setItem(KEY, JSON.stringify(session))

    expect(getSession()).toEqual(session)
  })

  it('aynı kayıt için her seferinde AYNI nesneyi verir (React yeniden çizim döngüsüne girmesin)', () => {
    saveSession(makeSession())

    expect(getSession()).toBe(getSession())
  })

  it('clearSession oturumu ve kaydı siler', () => {
    saveSession(makeSession())

    clearSession()

    expect(getSession()).toBeNull()
    expect(localStorage.getItem(KEY)).toBeNull()
  })

  it('yalnızca bilinen alanları saklar', () => {
    saveSession({ ...makeSession(), parola: 'sizmamali' } as ReturnType<typeof makeSession>)

    expect(Object.keys(JSON.parse(localStorage.getItem(KEY)!)).sort()).toEqual(['email', 'expiresAt', 'token'])
  })
})

describe('süresi dolan ya da bozuk oturum', () => {
  it('süresi dolmuş oturum "yok" sayılır', () => {
    saveSession(makeSession({ expiresAt: new Date(Date.now() - 1000).toISOString() }))

    expect(getSession()).toBeNull()
    expect(getToken()).toBeNull()
  })

  it('oturum açıkken süre dolarsa bir sonraki okumada oturum biter', () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-06-01T10:00:00Z'))
    saveSession(makeSession({ expiresAt: '2026-06-01T11:00:00Z' }))
    expect(getSession()).not.toBeNull()

    vi.setSystemTime(new Date('2026-06-01T11:00:01Z'))

    expect(getSession()).toBeNull()
  })

  it('tam bitiş anında oturum artık geçersizdir (sunucu da "exp" anında reddeder); bir an öncesi geçerlidir', () => {
    vi.useFakeTimers()
    saveSession(makeSession({ expiresAt: '2026-06-01T11:00:00.000Z' }))

    vi.setSystemTime(new Date('2026-06-01T10:59:59.999Z'))
    expect(getSession()).not.toBeNull()

    vi.setSystemTime(new Date('2026-06-01T11:00:00.000Z'))
    expect(getSession()).toBeNull()
  })

  it.each([
    ['JSON olmayan metin', 'bozuk{'],
    ['JSON ama nesne değil', '"merhaba"'],
    ['null', 'null'],
    ['token eksik', JSON.stringify({ email: 'a@b.c', expiresAt: futureDate() })],
    ['token boş', JSON.stringify({ token: '', email: 'a@b.c', expiresAt: futureDate() })],
    ['e-posta eksik', JSON.stringify({ token: 'x', expiresAt: futureDate() })],
    ['bitiş tarihi geçersiz', JSON.stringify({ token: 'x', email: 'a@b.c', expiresAt: 'dün' })],
    ['alan türü yanlış', JSON.stringify({ token: 123, email: 'a@b.c', expiresAt: futureDate() })],
  ])('bozuk kayıt (%s) çökertmez, oturum yok sayılır', (_name, raw) => {
    localStorage.setItem(KEY, raw)

    expect(getSession()).toBeNull()
  })
})

describe('abonelik', () => {
  it('giriş ve çıkışta abonelere haber verir', () => {
    const listener = vi.fn()
    subscribeSession(listener)

    saveSession(makeSession())
    expect(listener).toHaveBeenCalledTimes(1)

    clearSession()
    expect(listener).toHaveBeenCalledTimes(2)
  })

  it('abonelik bitince haber vermez', () => {
    const listener = vi.fn()
    const unsubscribe = subscribeSession(listener)

    unsubscribe()
    saveSession(makeSession())

    expect(listener).not.toHaveBeenCalled()
  })

  it('başka sekmede oturum değişirse ("storage" olayı) abonelere haber verir', () => {
    const listener = vi.fn()
    subscribeSession(listener)

    window.dispatchEvent(new StorageEvent('storage', { key: KEY }))

    expect(listener).toHaveBeenCalledTimes(1)
  })

  it('başka sekmede localStorage.clear() yapılırsa (key null) haber verir', () => {
    const listener = vi.fn()
    subscribeSession(listener)

    window.dispatchEvent(new StorageEvent('storage', { key: null }))

    expect(listener).toHaveBeenCalledTimes(1)
  })

  it('ilgisiz bir anahtarın değişmesi abonelere haber vermez', () => {
    const listener = vi.fn()
    subscribeSession(listener)

    window.dispatchEvent(new StorageEvent('storage', { key: 'baska-anahtar' }))

    expect(listener).not.toHaveBeenCalled()
  })
})

describe('localStorage çalışmıyorsa', () => {
  it('oturum sayfa açık kaldığı sürece bellekte tutulur', () => {
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new DOMException('Kota doldu', 'QuotaExceededError')
    })
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new DOMException('Erişim engellendi', 'SecurityError')
    })
    const session = makeSession()

    saveSession(session)

    expect(getSession()).toEqual(session)
  })

  it('çıkış yapınca bellekteki oturum da silinir', () => {
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new DOMException('Kota doldu', 'QuotaExceededError')
    })
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new DOMException('Erişim engellendi', 'SecurityError')
    })
    vi.spyOn(Storage.prototype, 'removeItem').mockImplementation(() => {
      throw new DOMException('Erişim engellendi', 'SecurityError')
    })
    saveSession(makeSession())

    clearSession()

    expect(getSession()).toBeNull()
  })
})
