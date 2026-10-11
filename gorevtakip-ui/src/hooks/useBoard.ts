import { useEffect, useState } from 'react'
import { getErrorMessage } from '../api/client'
import { getProject } from '../api/projects'
import { createTask, deleteTask, updateTaskStatus } from '../api/tasks'
import type { ProjectDetail, TaskStatus } from '../types'

// Seçili projenin panosunu (proje + görevleri) yönetir.
// onChanged: görev eklenip silinince sol paneldeki sayaçları tazelemek için dışarıdan gelir.
export function useBoard(projectId: number, onChanged: () => void) {
  const [project, setProject] = useState<ProjectDetail | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false

    getProject(projectId)
      .then((data) => {
        if (cancelled) return
        setProject(data)
        setError(null)
      })
      .catch((e) => {
        if (!cancelled) setError(getErrorMessage(e))
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })

    return () => {
      cancelled = true
    }
  }, [projectId])

  async function addTask(title: string): Promise<boolean> {
    try {
      const created = await createTask(projectId, title)
      setProject((prev) => (prev ? { ...prev, tasks: [...prev.tasks, created] } : prev))
      setError(null)
      onChanged()
      return true
    } catch (e) {
      setError(getErrorMessage(e))
      return false
    }
  }

  // İyimser güncelleme (optimistic update): kartı ekranda hemen taşırız, sunucu cevabını beklemeyiz.
  // Sunucu hata verirse eski haline döneriz. Böylece sürükle-bırak anında tepki verir.
  async function moveTask(taskId: number, status: TaskStatus) {
    if (!project) return
    const task = project.tasks.find((t) => t.id === taskId)
    if (!task || task.status === status) return

    const previous = project
    setProject({
      ...project,
      tasks: project.tasks.map((t) => (t.id === taskId ? { ...t, status } : t)),
    })

    try {
      await updateTaskStatus(taskId, status)
      setError(null)
    } catch (e) {
      setProject(previous)
      setError(getErrorMessage(e))
    }
  }

  async function removeTask(taskId: number) {
    try {
      await deleteTask(taskId)
      setProject((prev) =>
        prev ? { ...prev, tasks: prev.tasks.filter((t) => t.id !== taskId) } : prev,
      )
      setError(null)
      onChanged()
    } catch (e) {
      setError(getErrorMessage(e))
    }
  }

  return { project, loading, error, addTask, moveTask, removeTask }
}
