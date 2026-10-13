import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ComponentProps } from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { makeProject } from '../test/factories'
import { ProjectList } from './ProjectList'

type Props = ComponentProps<typeof ProjectList>

function setup(overrides: Partial<Props> = {}) {
  const props: Props = {
    projects: [
      makeProject(1, { name: 'Web Sitesi', taskCount: 3 }),
      makeProject(2, { name: 'Mobil Uygulama', taskCount: 0 }),
    ],
    selectedId: null,
    loading: false,
    error: null,
    onSelect: vi.fn(),
    onAdd: vi.fn().mockResolvedValue(true),
    onDelete: vi.fn(),
    ...overrides,
  }
  render(<ProjectList {...props} />)
  return { user: userEvent.setup(), props }
}

afterEach(() => {
  vi.restoreAllMocks() // window.confirm casusunu geri al
})

describe('ProjectList', () => {
  it('projeleri adları ve görev sayılarıyla listeler', () => {
    setup()

    expect(screen.getByText('Web Sitesi')).toBeInTheDocument()
    expect(screen.getByText('Mobil Uygulama')).toBeInTheDocument()
    expect(screen.getByText('3')).toBeInTheDocument()
    expect(screen.getByText('0')).toBeInTheDocument()
  })

  it('seçili projeyi aria-current ile işaretler', () => {
    setup({ selectedId: 2 })

    expect(screen.getByText('Mobil Uygulama').closest('button')).toHaveAttribute('aria-current', 'true')
    expect(screen.getByText('Web Sitesi').closest('button')).not.toHaveAttribute('aria-current')
  })

  it('projeye tıklayınca onSelect\'i kimliğiyle çağırır', async () => {
    const { user, props } = setup()

    await user.click(screen.getByText('Web Sitesi'))

    expect(props.onSelect).toHaveBeenCalledWith(1)
  })

  it('yüklenirken "Yükleniyor…" yazar, "Henüz proje yok" yazmaz', () => {
    setup({ projects: [], loading: true })

    expect(screen.getByText('Yükleniyor…')).toBeInTheDocument()
    expect(screen.queryByText('Henüz proje yok.')).not.toBeInTheDocument()
  })

  it('yükleme bitti ve proje yoksa "Henüz proje yok." yazar', () => {
    setup({ projects: [] })

    expect(screen.getByText('Henüz proje yok.')).toBeInTheDocument()
  })

  it('hata varsa uyarı olarak gösterir', () => {
    setup({ error: 'API\'ye ulaşılamadı.' })

    expect(screen.getByRole('alert')).toHaveTextContent('API\'ye ulaşılamadı.')
  })

  it('ekleme formu onAdd\'i girilen adla çağırır', async () => {
    const { user, props } = setup()

    await user.type(screen.getByLabelText('Yeni proje adı'), 'Blog')
    await user.click(screen.getByRole('button', { name: 'Ekle' }))

    expect(props.onAdd).toHaveBeenCalledWith('Blog')
  })

  describe('silme', () => {
    it('önce onay sorar; onaylanırsa onDelete çağrılır', async () => {
      const confirm = vi.spyOn(window, 'confirm').mockReturnValue(true)
      const { user, props } = setup()

      await user.click(screen.getByRole('button', { name: 'Web Sitesi projesini sil' }))

      expect(confirm).toHaveBeenCalledWith('"Web Sitesi" projesi ve içindeki 3 görev silinsin mi?')
      expect(props.onDelete).toHaveBeenCalledWith(1)
    })

    it('onaylanmazsa onDelete çağrılmaz', async () => {
      vi.spyOn(window, 'confirm').mockReturnValue(false)
      const { user, props } = setup()

      await user.click(screen.getByRole('button', { name: 'Web Sitesi projesini sil' }))

      expect(props.onDelete).not.toHaveBeenCalled()
    })
  })
})
