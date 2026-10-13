import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { AddForm } from './AddForm'

// Testler kullanıcı gibi davranır: etikete göre kutuyu bulur, yazar, tıklar.
// Bileşenin iç state'ine ya da CSS sınıflarına bakmayız.
function setup(onSubmit: (value: string) => Promise<boolean> = vi.fn().mockResolvedValue(true)) {
  render(
    <AddForm placeholder="Yeni görev başlığı" buttonLabel="Görev ekle" maxLength={200} onSubmit={onSubmit} />,
  )
  return {
    user: userEvent.setup(),
    onSubmit,
    input: screen.getByLabelText('Yeni görev başlığı'),
    button: screen.getByRole('button', { name: 'Görev ekle' }),
  }
}

describe('AddForm', () => {
  it('başlangıçta kutu boştur ve buton kapalıdır', () => {
    const { input, button } = setup()

    expect(input).toHaveValue('')
    expect(button).toBeDisabled()
  })

  it('sadece boşluk yazılırsa buton kapalı kalır', async () => {
    const { user, input, button } = setup()

    await user.type(input, '   ')

    expect(button).toBeDisabled()
  })

  it('metin yazılınca buton açılır', async () => {
    const { user, input, button } = setup()

    await user.type(input, 'Rapor yaz')

    expect(button).toBeEnabled()
  })

  it('gönderince metni kırpıp onSubmit çağırır; başarılıysa kutuyu temizler', async () => {
    const { user, input, button, onSubmit } = setup()

    await user.type(input, '  Rapor yaz  ')
    await user.click(button)

    expect(onSubmit).toHaveBeenCalledTimes(1)
    expect(onSubmit).toHaveBeenCalledWith('Rapor yaz')
    await waitFor(() => expect(input).toHaveValue(''))
  })

  it('Enter tuşuyla da gönderir', async () => {
    const { user, input, onSubmit } = setup()

    await user.type(input, 'Rapor yaz{Enter}')

    expect(onSubmit).toHaveBeenCalledWith('Rapor yaz')
  })

  it('onSubmit false dönerse (ekleme başarısız) yazılan metni korur', async () => {
    const { user, input, button, onSubmit } = setup(vi.fn().mockResolvedValue(false))

    await user.type(input, 'Rapor yaz')
    await user.click(button)

    await waitFor(() => expect(onSubmit).toHaveBeenCalled())
    await waitFor(() => expect(button).toBeEnabled()) // istek bitti
    expect(input).toHaveValue('Rapor yaz')
  })

  it('istek sürerken butonu kapatır, bitince yeniden açar', async () => {
    let finish: (ok: boolean) => void = () => {}
    const onSubmit = vi.fn(
      () =>
        new Promise<boolean>((resolve) => {
          finish = resolve
        }),
    )
    const { user, input, button } = setup(onSubmit)

    await user.type(input, 'Rapor yaz')
    await user.click(button)

    expect(button).toBeDisabled() // istek hâlâ sürüyor

    finish(false)

    await waitFor(() => expect(button).toBeEnabled())
  })

  it('maxLength sınırını giriş kutusuna uygular', () => {
    const { input } = setup()

    expect(input).toHaveAttribute('maxlength', '200')
  })

  it('istek sürerken yazılan yeni metni, istek bitince silmez', async () => {
    let finish: (ok: boolean) => void = () => {}
    const onSubmit = vi.fn(
      () =>
        new Promise<boolean>((resolve) => {
          finish = resolve
        }),
    )
    const { user, input, button } = setup(onSubmit)

    await user.type(input, 'Birinci')
    await user.click(button)
    // İlk istek henüz bitmedi; kullanıcı sıradaki görevi yazmaya başladı
    await user.clear(input)
    await user.type(input, 'İkinci')

    finish(true)

    await waitFor(() => expect(button).toBeEnabled())
    expect(input).toHaveValue('İkinci')
  })
})
