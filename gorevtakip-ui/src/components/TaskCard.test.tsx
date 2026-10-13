import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { makeTask } from '../test/factories'
import type { Task } from '../types'
import { TaskCard } from './TaskCard'

function setup(task: Task) {
  const onMove = vi.fn()
  const onDelete = vi.fn()
  render(<TaskCard task={task} onMove={onMove} onDelete={onDelete} />)
  return { user: userEvent.setup(), onMove, onDelete }
}

describe('TaskCard', () => {
  it('başlığı ve (varsa) açıklamayı gösterir', () => {
    setup(makeTask(1, { title: 'Rapor yaz', description: 'Cuma gününe kadar' }))

    expect(screen.getByRole('heading', { name: 'Rapor yaz' })).toBeInTheDocument()
    expect(screen.getByText('Cuma gününe kadar')).toBeInTheDocument()
  })

  it('açıklama yoksa açıklama alanı çizilmez', () => {
    setup(makeTask(1, { title: 'Rapor yaz', description: null }))

    // Başlık dışında metin paragrafı (<p>) çizilmemeli
    expect(screen.getByRole('article').querySelector('p')).toBeNull()
  })

  describe('taşıma butonları', () => {
    it('"Yapılacak" kartında ← kapalı, → açıktır ve "Devam ediyor"a taşır', async () => {
      const { user, onMove } = setup(makeTask(1, { status: 'Todo' }))

      expect(screen.getByRole('button', { name: 'Sola taşınamaz' })).toBeDisabled()
      const right = screen.getByRole('button', { name: '"Devam ediyor" kolonuna taşı' })
      expect(right).toBeEnabled()

      await user.click(right)

      expect(onMove).toHaveBeenCalledWith(1, 'InProgress')
    })

    it('"Devam ediyor" kartında iki yön de açıktır', async () => {
      const { user, onMove } = setup(makeTask(2, { status: 'InProgress' }))

      await user.click(screen.getByRole('button', { name: '"Yapılacak" kolonuna taşı' }))
      await user.click(screen.getByRole('button', { name: '"Tamamlandı" kolonuna taşı' }))

      expect(onMove).toHaveBeenNthCalledWith(1, 2, 'Todo')
      expect(onMove).toHaveBeenNthCalledWith(2, 2, 'Done')
    })

    it('"Tamamlandı" kartında → kapalı, ← açıktır', async () => {
      const { user, onMove } = setup(makeTask(3, { status: 'Done' }))

      expect(screen.getByRole('button', { name: 'Sağa taşınamaz' })).toBeDisabled()

      await user.click(screen.getByRole('button', { name: '"Devam ediyor" kolonuna taşı' }))

      expect(onMove).toHaveBeenCalledWith(3, 'InProgress')
    })
  })

  it('Sil butonu onDelete\'i görevin kimliğiyle çağırır', async () => {
    const { user, onDelete } = setup(makeTask(7, { title: 'Rapor yaz' }))

    await user.click(screen.getByRole('button', { name: '"Rapor yaz" görevini sil' }))

    expect(onDelete).toHaveBeenCalledWith(7)
  })

  // jsdom'da sürükle-bırak olayları eksiktir; Testing Library dataTransfer'ı olaya elle ekler.
  // Gerçek sürükle-bırak davranışı e2e/board.spec.ts'te gerçek tarayıcıyla sınanır.
  it('sürüklenebilir ve sürükleme başlayınca görev kimliğini dataTransfer\'a yazar', () => {
    setup(makeTask(5))
    const card = screen.getByRole('article')
    const dataTransfer = { setData: vi.fn(), effectAllowed: '' }

    fireEvent.dragStart(card, { dataTransfer })

    expect(card).toHaveAttribute('draggable', 'true')
    expect(dataTransfer.setData).toHaveBeenCalledWith('text/plain', '5')
  })
})
