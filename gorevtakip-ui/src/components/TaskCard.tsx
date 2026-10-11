import type { DragEvent } from 'react'
import { COLUMNS } from '../types'
import type { Task, TaskStatus } from '../types'

interface Props {
  task: Task
  onMove: (taskId: number, status: TaskStatus) => void
  onDelete: (taskId: number) => void
}

export function TaskCard({ task, onMove, onDelete }: Props) {
  // Kartın bulunduğu kolonun sol ve sağ komşusu (kenardaysa tanımsız olur)
  const index = COLUMNS.findIndex((c) => c.status === task.status)
  const previous = COLUMNS[index - 1]
  const next = COLUMNS[index + 1]

  function handleDragStart(event: DragEvent<HTMLElement>) {
    // Hangi kartın sürüklendiğini bırakılacak kolona id olarak taşıyoruz
    event.dataTransfer.setData('text/plain', String(task.id))
    event.dataTransfer.effectAllowed = 'move'
  }

  return (
    <article className="card" draggable onDragStart={handleDragStart}>
      <h3 className="card-title">{task.title}</h3>
      {task.description && <p className="card-desc">{task.description}</p>}

      <div className="card-actions">
        <button
          className="icon-btn"
          disabled={!previous}
          aria-label={previous ? `"${previous.title}" kolonuna taşı` : 'Sola taşınamaz'}
          onClick={() => previous && onMove(task.id, previous.status)}
        >
          ←
        </button>
        <button
          className="icon-btn"
          disabled={!next}
          aria-label={next ? `"${next.title}" kolonuna taşı` : 'Sağa taşınamaz'}
          onClick={() => next && onMove(task.id, next.status)}
        >
          →
        </button>
        <button className="icon-btn danger" aria-label={`"${task.title}" görevini sil`} onClick={() => onDelete(task.id)}>
          Sil
        </button>
      </div>
    </article>
  )
}
