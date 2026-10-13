import { fireEvent, render, screen, within } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { makeTask } from '../test/factories'
import type { Task } from '../types'
import { Column } from './Column'

function setup(tasks: Task[] = []) {
  const onMove = vi.fn()
  const onDelete = vi.fn()
  render(<Column status="Done" title="Tamamlandı" tasks={tasks} onMove={onMove} onDelete={onDelete} />)
  return { onMove, onDelete, column: screen.getByRole('region', { name: 'Tamamlandı' }) }
}

// jsdom'da DragEvent yoktur; Testing Library olaya dataTransfer'ı elle ekler.
// Gerçek sürükle-bırak e2e/board.spec.ts'te gerçek tarayıcıyla sınanır.
describe('Column', () => {
  it('başlığı ve görev sayısını gösterir', () => {
    const { column } = setup([makeTask(1, { status: 'Done' }), makeTask(2, { status: 'Done' })])

    expect(within(column).getByRole('heading', { name: 'Tamamlandı' })).toBeInTheDocument()
    expect(within(column).getByText('2')).toBeInTheDocument()
  })

  it('görev kartlarını çizer', () => {
    const { column } = setup([makeTask(1, { title: 'Birinci' }), makeTask(2, { title: 'İkinci' })])

    expect(within(column).getAllByRole('article')).toHaveLength(2)
    expect(within(column).getByText('Birinci')).toBeInTheDocument()
    expect(within(column).getByText('İkinci')).toBeInTheDocument()
  })

  it('boşken "Buraya kart sürükle" yazar', () => {
    const { column } = setup()
    expect(within(column).getByText('Buraya kart sürükle')).toBeInTheDocument()
  })

  it('doluyken "Buraya kart sürükle" yazmaz', () => {
    setup([makeTask(1)])
    expect(screen.queryByText('Buraya kart sürükle')).not.toBeInTheDocument()
  })

  it('kart bırakılınca onMove\'u kartın kimliği ve kolonun durumuyla çağırır', () => {
    const { column, onMove } = setup()

    fireEvent.drop(column, { dataTransfer: { getData: () => '7' } })

    expect(onMove).toHaveBeenCalledWith(7, 'Done')
  })

  it.each(['abc', '', '0', '-3', '1.5'])(
    'bırakılan veri geçerli bir kimlik değilse (%j) onMove çağrılmaz',
    (garbage) => {
      const { column, onMove } = setup()

      fireEvent.drop(column, { dataTransfer: { getData: () => garbage } })

      expect(onMove).not.toHaveBeenCalled()
    },
  )

  it('üzerine kart sürüklenirken vurgulanır, bırakılınca vurgu kalkar', () => {
    const { column } = setup()
    const dataTransfer = { dropEffect: 'none', getData: () => '7' }

    fireEvent.dragOver(column, { dataTransfer })
    expect(column).toHaveClass('over')

    fireEvent.drop(column, { dataTransfer })
    expect(column).not.toHaveClass('over')
  })

  it('kart kolonun dışına sürüklenince vurgu kalkar', () => {
    const { column } = setup()

    fireEvent.dragOver(column, { dataTransfer: { dropEffect: 'none' } })
    expect(column).toHaveClass('over')

    fireEvent.dragLeave(column)
    expect(column).not.toHaveClass('over')
  })
})
