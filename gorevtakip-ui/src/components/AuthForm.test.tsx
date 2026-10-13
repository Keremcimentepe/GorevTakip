import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ComponentProps } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { AuthForm } from './AuthForm'

function setup(props: Partial<ComponentProps<typeof AuthForm>> = {}) {
  const onLogin = vi.fn().mockResolvedValue(null)
  const onRegister = vi.fn().mockResolvedValue(null)
  render(<AuthForm onLogin={onLogin} onRegister={onRegister} {...props} />)
  return {
    user: userEvent.setup(),
    onLogin,
    onRegister,
    email: screen.getByLabelText('E-posta'),
    password: screen.getByLabelText('Şifre'),
    submit: () => screen.getByRole('button', { name: /^(Giriş yap|Kayıt ol)$/ }),
  }
}

describe('AuthForm: giriş modu', () => {
  it('varsayılan olarak giriş formu açılır', () => {
    const { submit } = setup()

    expect(screen.getByRole('heading', { name: 'Giriş yap' })).toBeInTheDocument()
    expect(submit()).toHaveTextContent('Giriş yap')
    expect(screen.queryByText('En az 8 karakter olmalı.')).not.toBeInTheDocument()
  })

  it('alanlar boşken buton kapalıdır; ikisi de dolunca açılır', async () => {
    const { user, email, password, submit } = setup()
    expect(submit()).toBeDisabled()

    await user.type(email, 'ali@ornek.com')
    expect(submit()).toBeDisabled()

    await user.type(password, 'Sifre1234!')
    expect(submit()).toBeEnabled()
  })

  it('sadece şifre yazılırsa (e-posta boşken) buton kapalı kalır', async () => {
    const { user, password, submit } = setup()

    await user.type(password, 'Sifre1234!')

    expect(submit()).toBeDisabled()
  })

  it('e-posta ve şifreyi onLogin\'e verir; şifredeki baş/son boşluk korunur', async () => {
    const { user, email, password, submit, onLogin, onRegister } = setup()

    await user.type(email, 'ali@ornek.com')
    await user.type(password, ' sifre ile bosluk ')
    await user.click(submit())

    expect(onLogin).toHaveBeenCalledWith('ali@ornek.com', ' sifre ile bosluk ')
    expect(onRegister).not.toHaveBeenCalled()
  })

  it('Enter tuşuyla da gönderilir', async () => {
    const { user, email, password, onLogin } = setup()

    await user.type(email, 'ali@ornek.com')
    await user.type(password, 'Sifre1234!{Enter}')

    expect(onLogin).toHaveBeenCalledTimes(1)
  })

  it('hata dönerse mesajı uyarı olarak gösterir ve yazılanları silmez', async () => {
    const onLogin = vi.fn().mockResolvedValue('E-posta veya şifre hatalı.')
    const { user, email, password, submit } = setup({ onLogin })

    await user.type(email, 'ali@ornek.com')
    await user.type(password, 'yanlis')
    await user.click(submit())

    expect(await screen.findByRole('alert')).toHaveTextContent('E-posta veya şifre hatalı.')
    expect(email).toHaveValue('ali@ornek.com')
    expect(password).toHaveValue('yanlis')
    expect(submit()).toBeEnabled() // yeniden denenebilir
  })

  it('istek sürerken buton kapanır, çift tıklama ikinci istek göndermez', async () => {
    let finish: (value: string | null) => void = () => {}
    const onLogin = vi.fn().mockReturnValue(new Promise<string | null>((resolve) => (finish = resolve)))
    const { user, email, password, submit } = setup({ onLogin })
    await user.type(email, 'ali@ornek.com')
    await user.type(password, 'Sifre1234!')

    await user.click(submit())
    await user.click(submit())

    expect(submit()).toBeDisabled()
    expect(onLogin).toHaveBeenCalledTimes(1)

    finish('Bir hata')
    await waitFor(() => expect(submit()).toBeEnabled())
  })

  it('yeni deneme başlar başlamaz (cevap beklenirken) eski hata mesajı kalkar', async () => {
    const onLogin = vi
      .fn()
      .mockResolvedValueOnce('E-posta veya şifre hatalı.')
      .mockReturnValueOnce(new Promise<string | null>(() => {})) // ikinci deneme hiç bitmesin
    const { user, email, password, submit } = setup({ onLogin })
    await user.type(email, 'ali@ornek.com')
    await user.type(password, 'yanlis')
    await user.click(submit())
    await screen.findByRole('alert')

    await user.click(submit())

    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('verilen bilgi mesajını (ör. oturum süresi doldu) gösterir', () => {
    setup({ notice: 'Oturumun süresi doldu.' })

    expect(screen.getByRole('status')).toHaveTextContent('Oturumun süresi doldu.')
  })

  it('bilgi mesajı yoksa durum alanı çizilmez', () => {
    setup()

    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })
})

describe('AuthForm: kayıt modu', () => {
  async function openRegister() {
    const utils = setup()
    await utils.user.click(screen.getByRole('button', { name: 'Hesabın yok mu? Kayıt ol' }))
    return utils
  }

  it('bağlantı butonu kayıt formuna geçirir; şifre kuralı ipucu görünür', async () => {
    const { submit } = await openRegister()

    expect(screen.getByRole('heading', { name: 'Hesap oluştur' })).toBeInTheDocument()
    expect(submit()).toHaveTextContent('Kayıt ol')
    expect(screen.getByLabelText('Şifre')).toHaveAccessibleDescription('En az 8 karakter olmalı.')
  })

  it('onRegister\'ı çağırır, onLogin\'i çağırmaz', async () => {
    const { user, email, password, submit, onLogin, onRegister } = await openRegister()

    await user.type(email, 'ali@ornek.com')
    await user.type(password, 'Sifre1234!')
    await user.click(submit())

    expect(onRegister).toHaveBeenCalledWith('ali@ornek.com', 'Sifre1234!')
    expect(onLogin).not.toHaveBeenCalled()
  })

  it('sunucunun doğrulama mesajını gösterir', async () => {
    const onRegister = vi.fn().mockResolvedValue('Şifre en az 8 karakter olmalı.')
    const utils = setup({ onRegister })
    await utils.user.click(screen.getByRole('button', { name: 'Hesabın yok mu? Kayıt ol' }))
    await utils.user.type(utils.email, 'ali@ornek.com')
    await utils.user.type(utils.password, 'kisa')
    await utils.user.click(utils.submit())

    expect(await screen.findByRole('alert')).toHaveTextContent('Şifre en az 8 karakter olmalı.')
  })

  it('moda geçince eski hata silinir, yazılan e-posta korunur; geri dönülebilir', async () => {
    const onLogin = vi.fn().mockResolvedValue('E-posta veya şifre hatalı.')
    const { user, email, password, submit } = setup({ onLogin })
    await user.type(email, 'ali@ornek.com')
    await user.type(password, 'yanlis')
    await user.click(submit())
    await screen.findByRole('alert')

    await user.click(screen.getByRole('button', { name: 'Hesabın yok mu? Kayıt ol' }))
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    expect(email).toHaveValue('ali@ornek.com')

    await user.click(screen.getByRole('button', { name: 'Zaten hesabın var mı? Giriş yap' }))
    expect(screen.getByRole('heading', { name: 'Giriş yap' })).toBeInTheDocument()
  })
})
