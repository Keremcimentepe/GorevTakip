import { useState } from 'react'
import type { FormEvent } from 'react'

interface Props {
  // Örn. "Oturumun süresi doldu…" gibi, neden bu ekranı gördüğünü anlatan bilgi
  notice?: string | null
  // Başarılıysa null, başarısızsa gösterilecek hata mesajını döner
  onLogin: (email: string, password: string) => Promise<string | null>
  onRegister: (email: string, password: string) => Promise<string | null>
}

// Giriş ve kayıt aynı formu paylaşır; alttaki bağlantı butonu ikisi arasında geçiş yapar.
// Alan kuralları (e-posta biçimi, şifre uzunluğu) sunucuda denetlenir ve mesajı buraya gelir:
// kuralı iki yerde tutmayız, ikisi birbirinden ayrışmaz.
export function AuthForm({ notice, onLogin, onRegister }: Props) {
  const [mode, setMode] = useState<'login' | 'register'>('login')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const isLogin = mode === 'login'

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const trimmedEmail = email.trim()
    if (!trimmedEmail || !password || busy) return

    setBusy(true)
    setError(null)
    // Şifreyi kırpmayız: baştaki/sondaki boşluk şifrenin parçası olabilir
    const message = await (isLogin ? onLogin : onRegister)(trimmedEmail, password)
    setBusy(false)
    setError(message)
  }

  function switchMode() {
    setMode(isLogin ? 'register' : 'login')
    setError(null)
  }

  return (
    <main className="auth">
      {/* noValidate: tarayıcının kendi (başka dilde) uyarısı yerine sunucunun Türkçe mesajı gösterilsin */}
      <form className="auth-card" onSubmit={handleSubmit} aria-labelledby="auth-title" noValidate>
        <p className="logo">GörevTakip</p>
        <h1 id="auth-title">{isLogin ? 'Giriş yap' : 'Hesap oluştur'}</h1>

        {notice && (
          <p className="notice" role="status">
            {notice}
          </p>
        )}

        <label className="field">
          <span>E-posta</span>
          <input
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            autoComplete="email"
            maxLength={254}
          />
        </label>

        <label className="field">
          <span>Şifre</span>
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete={isLogin ? 'current-password' : 'new-password'}
            aria-describedby={isLogin ? undefined : 'password-hint'}
            maxLength={128}
          />
        </label>
        {!isLogin && (
          <p id="password-hint" className="muted">
            En az 8 karakter olmalı.
          </p>
        )}

        {error && (
          <p className="error" role="alert">
            {error}
          </p>
        )}

        <button type="submit" className="primary" disabled={busy || email.trim() === '' || password === ''}>
          {isLogin ? 'Giriş yap' : 'Kayıt ol'}
        </button>
        <button type="button" className="link" onClick={switchMode}>
          {isLogin ? 'Hesabın yok mu? Kayıt ol' : 'Zaten hesabın var mı? Giriş yap'}
        </button>
      </form>
    </main>
  )
}
