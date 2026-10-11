import { useState } from 'react'
import type { DragEvent } from 'react'
import type { Task, TaskStatus } from '../types'
import { TaskCard } from './TaskCard'

interface Props {
  status: TaskStatus
  title: string
  tasks: Task[]
  onMove: (taskId: number, status: TaskStatus) => void
  onDelete: (taskId: number) => void
}

export function Column({ status, title, tasks, onMove, onDelete }: Props) {
  // Üzerine kart sürüklenirken kolonu vurgulamak için
  const [isOver, setIsOver] = useState(false)

  function handleDragOver(event: DragEvent<HTMLElement>) {
    event.preventDefault() // Tarayıcı varsayılan olarak bırakmaya izin vermez; bu satır izin verir
    event.dataTransfer.dropEffect = 'move'
    setIsOver(true)
  }

  function handleDragLeave(event: DragEvent<HTMLElement>) {
    // Kolonun içindeki çocuk elemanların üzerine geçmek de "leave" tetikler; onları yok say
    if (!event.currentTarget.contains(event.relatedTarget as Node | null)) {
      setIsOver(false)
    }
  }

  function handleDrop(event: DragEvent<HTMLElement>) {
    event.preventDefault()
    setIsOver(false)
    const taskId = Number(event.dataTransfer.getData('text/plain'))
    if (Number.isInteger(taskId) && taskId > 0) {
      onMove(taskId, status)
    }
  }

  return (
    <section
      className={isOver ? 'column over' : 'column'}
      aria-label={title}
      onDragOver={handleDragOver}
      onDragLeave={handleDragLeave}
      onDrop={handleDrop}
    >
      <header className="column-header">
        <h2>{title}</h2>
        <span className="badge">{tasks.length}</span>
      </header>

      <div className="column-body">
        {tasks.map((task) => (
          <TaskCard key={task.id} task={task} onMove={onMove} onDelete={onDelete} />
        ))}
        {tasks.length === 0 && <p className="muted center">Buraya kart sürükle</p>}
      </div>
    </section>
  )
}
