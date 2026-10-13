import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { login, register } from './auth'
import { clearSession, saveSession } from './session'

const fetchMock = vi.fn()

const AUTH_RESPONSE = { token: 'jwt', expiresAt: '2030-01-01T00:00:00Z', email: 'ali@ornek.com' }

beforeEach(() => {
  fetchMock.mockReset()
  fetchMock.mockResolvedValue(
    new Response(JSON.stringify(AUTH_RESPONSE), {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    }),
  )
  vi.stubGlobal('fetch', fetchMock)
})

afterEach(() => {
  clearSession()
  vi.unstubAllGlobals()
})

describe('login / register', () => {
  it('login: /auth/login adresine e-posta ve şifreyi POST eder, cevabı döner', async () => {
    await expect(login('ali@ornek.com', 'Sifre1234!')).resolves.toEqual(AUTH_RESPONSE)

    const [url, options] = fetchMock.mock.calls[0]
    expect(String(url)).toMatch(/\/auth\/login$/)
    expect(options.method).toBe('POST')
    expect(JSON.parse(options.body)).toEqual({ email: 'ali@ornek.com', password: 'Sifre1234!' })
  })

  it('register: /auth/register adresine POST eder', async () => {
    await register('ali@ornek.com', 'Sifre1234!')

    const [url, options] = fetchMock.mock.calls[0]
    expect(String(url)).toMatch(/\/auth\/register$/)
    expect(options.method).toBe('POST')
    expect(JSON.parse(options.body)).toEqual({ email: 'ali@ornek.com', password: 'Sifre1234!' })
  })

  it.each([
    ['login', () => login('a@b.c', 'Sifre1234!')],
    ['register', () => register('a@b.c', 'Sifre1234!')],
  ])('%s: eski bir oturum kalmış olsa bile Authorization başlığı göndermez', async (_name, call) => {
    saveSession({ token: 'eski-token', email: 'eski@ornek.com', expiresAt: '2099-01-01T00:00:00Z' })

    await call()

    const [, options] = fetchMock.mock.calls[0]
    expect(options.headers).not.toHaveProperty('Authorization')
  })
})
