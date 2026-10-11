import { useState } from 'react'
import './App.css'
import { Board } from './components/Board'
import { ProjectList } from './components/ProjectList'
import { useProjects } from './hooks/useProjects'

export default function App() {
  const { projects, loading, error, reload, addProject, removeProject } = useProjects()
  const [selectedId, setSelectedId] = useState<number | null>(null)

  async function handleAddProject(name: string) {
    const created = await addProject(name)
    if (created) setSelectedId(created.id) // Yeni projeyi hemen aç
    return created !== null
  }

  async function handleDeleteProject(id: number) {
    const deleted = await removeProject(id)
    if (deleted && selectedId === id) setSelectedId(null)
  }

  return (
    <div className="app">
      <aside className="sidebar">
        <p className="logo">GörevTakip</p>
        <ProjectList
          projects={projects}
          selectedId={selectedId}
          loading={loading}
          error={error}
          onSelect={setSelectedId}
          onAdd={handleAddProject}
          onDelete={handleDeleteProject}
        />
      </aside>

      <main className="main">
        {selectedId === null ? (
          <p className="muted">Soldan bir proje seç ya da yeni bir proje oluştur.</p>
        ) : (
          // key: proje değişince Board sıfırdan kurulur, önceki projenin state'i sızmaz
          <Board key={selectedId} projectId={selectedId} onTasksChanged={reload} />
        )}
      </main>
    </div>
  )
}
