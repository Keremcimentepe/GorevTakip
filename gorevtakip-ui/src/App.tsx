import { useState } from 'react'
import './App.css'
import { AuthForm } from './components/AuthForm'
import { Board } from './components/Board'
import { ProjectList } from './components/ProjectList'
import { useAuth } from './hooks/useAuth'
import { useProjects } from './hooks/useProjects'

// Kapı: oturum yoksa giriş/kayıt formu, varsa çalışma alanı.
export default function App() {
  const { session, notice, login, register, logout } = useAuth()

  if (!session) return <AuthForm notice={notice} onLogin={login} onRegister={register} />

  // key: kullanıcı değişirse (ör. başka sekmede başka hesapla girildi) çalışma alanı sıfırdan kurulur,
  // önceki kullanıcının projeleri bir an bile görünmez.
  return <Workspace key={session.email} email={session.email} onLogout={logout} />
}

interface WorkspaceProps {
  email: string
  onLogout: () => void
}

function Workspace({ email, onLogout }: WorkspaceProps) {
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

        <div className="account">
          <p className="account-email" title={email}>
            {email}
          </p>
          <button type="button" onClick={onLogout}>
            Çıkış yap
          </button>
        </div>
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
