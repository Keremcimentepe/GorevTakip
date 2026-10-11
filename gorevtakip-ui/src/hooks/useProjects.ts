import { useCallback, useEffect, useState } from 'react'
import { getErrorMessage } from '../api/client'
import { createProject, deleteProject, getProjects } from '../api/projects'
import type { Project } from '../types'

// Sol paneldeki proje listesinin durumunu ve işlemlerini tek yerde toplar.
// Bileşenler "nasıl çekilir / nasıl saklanır" ile ilgilenmez, sadece bu hook'u kullanır.
export function useProjects() {
  const [projects, setProjects] = useState<Project[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  // İlk yükleme: bileşen ekrana gelince bir kez çalışır.
  // `cancelled` bayrağı: bileşen yükleme bitmeden kaybolursa eski cevabın state'e yazılmasını engeller.
  useEffect(() => {
    let cancelled = false

    getProjects()
      .then((data) => {
        if (cancelled) return
        setProjects(data)
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
  }, [])

  // Listeyi yeniden çeker. Panoda görev eklenip silinince görev sayacı güncellensin diye de kullanılır.
  const reload = useCallback(async () => {
    try {
      setProjects(await getProjects())
      setError(null)
    } catch (e) {
      setError(getErrorMessage(e))
    }
  }, [])

  // Başarılıysa oluşan projeyi, başarısızsa null döner.
  async function addProject(name: string): Promise<Project | null> {
    try {
      const created = await createProject(name)
      await reload()
      return created
    } catch (e) {
      setError(getErrorMessage(e))
      return null
    }
  }

  // Başarılıysa true döner.
  async function removeProject(id: number): Promise<boolean> {
    try {
      await deleteProject(id)
      await reload()
      return true
    } catch (e) {
      setError(getErrorMessage(e))
      return false
    }
  }

  return { projects, loading, error, reload, addProject, removeProject }
}
