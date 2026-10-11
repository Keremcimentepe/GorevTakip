import { useBoard } from '../hooks/useBoard'
import { COLUMNS } from '../types'
import { AddForm } from './AddForm'
import { Column } from './Column'

interface Props {
  projectId: number
  onTasksChanged: () => void
}

export function Board({ projectId, onTasksChanged }: Props) {
  const { project, loading, error, addTask, moveTask, removeTask } = useBoard(projectId, onTasksChanged)

  if (loading) return <p className="muted">Pano yükleniyor…</p>

  // Proje hiç yüklenemediyse (ör. 404) panoyu çizemeyiz, sadece hatayı gösteririz
  if (!project) {
    return (
      <p className="error" role="alert">
        {error ?? 'Proje yüklenemedi.'}
      </p>
    )
  }

  return (
    <div>
      <header className="board-header">
        <h1>{project.name}</h1>
        {project.description && <p className="muted">{project.description}</p>}
      </header>

      {/* Backend'de görev başlığı en fazla 200 karakter */}
      <AddForm placeholder="Yeni görev başlığı" buttonLabel="Görev ekle" maxLength={200} onSubmit={addTask} />

      {error && (
        <p className="error" role="alert">
          {error}
        </p>
      )}

      <div className="board">
        {COLUMNS.map((column) => (
          <Column
            key={column.status}
            status={column.status}
            title={column.title}
            tasks={project.tasks.filter((t) => t.status === column.status)}
            onMove={moveTask}
            onDelete={removeTask}
          />
        ))}
      </div>
    </div>
  )
}
