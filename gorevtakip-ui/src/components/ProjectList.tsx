import type { Project } from '../types'
import { AddForm } from './AddForm'

interface Props {
  projects: Project[]
  selectedId: number | null
  loading: boolean
  error: string | null
  onSelect: (id: number) => void
  onAdd: (name: string) => Promise<boolean>
  onDelete: (id: number) => void
}

export function ProjectList({ projects, selectedId, loading, error, onSelect, onAdd, onDelete }: Props) {
  function confirmDelete(project: Project) {
    const message = `"${project.name}" projesi ve içindeki ${project.taskCount} görev silinsin mi?`
    if (window.confirm(message)) onDelete(project.id)
  }

  return (
    <section>
      {/* Backend'de proje adı en fazla 100 karakter */}
      <AddForm placeholder="Yeni proje adı" buttonLabel="Ekle" maxLength={100} onSubmit={onAdd} />

      {error && (
        <p className="error" role="alert">
          {error}
        </p>
      )}

      {loading && <p className="muted">Yükleniyor…</p>}
      {!loading && projects.length === 0 && <p className="muted">Henüz proje yok.</p>}

      <ul className="project-list">
        {projects.map((project) => (
          <li key={project.id} className={project.id === selectedId ? 'project selected' : 'project'}>
            <button
              className="project-select"
              aria-current={project.id === selectedId ? 'true' : undefined}
              onClick={() => onSelect(project.id)}
            >
              <span className="project-name">{project.name}</span>
              <span className="badge">{project.taskCount}</span>
            </button>
            <button
              className="icon-btn"
              aria-label={`${project.name} projesini sil`}
              onClick={() => confirmDelete(project)}
            >
              ×
            </button>
          </li>
        ))}
      </ul>
    </section>
  )
}
