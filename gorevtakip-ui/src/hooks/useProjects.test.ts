import { act, renderHook, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../api/client'
import { createProject, deleteProject, getProjects } from '../api/projects'
import { makeProject } from '../test/factories'
import type { Project } from '../types'
import { useProjects } from './useProjects'

vi.mock('../api/projects')

beforeEach(() => {
  vi.resetAllMocks()
})

async function renderLoadedProjects() {
  const rendered = renderHook(() => useProjects())
  await waitFor(() => expect(rendered.result.current.loading).toBe(false))
  return rendered
}

describe('useProjects: yükleme', () => {
  it('açılışta projeleri yükler', async () => {
    vi.mocked(getProjects).mockResolvedValue([makeProject(1), makeProject(2)])

    const { result } = await renderLoadedProjects()

    expect(result.current.projects).toHaveLength(2)
    expect(result.current.error).toBeNull()
  })

  it('yükleme sürerken loading true döner', () => {
    vi.mocked(getProjects).mockReturnValue(new Promise<Project[]>(() => {})) // hiç bitmeyen istek

    const { result } = renderHook(() => useProjects())

    expect(result.current.loading).toBe(true)
    expect(result.current.projects).toEqual([])
  })

  it('yükleme başarısız olursa hata mesajını verir', async () => {
    vi.mocked(getProjects).mockRejectedValue(new ApiError("API'ye ulaşılamadı.", 0))

    const { result } = await renderLoadedProjects()

    expect(result.current.projects).toEqual([])
    expect(result.current.error).toBe("API'ye ulaşılamadı.")
  })
})

describe('useProjects: addProject', () => {
  it('projeyi oluşturur, listeyi yeniler ve oluşan projeyi döner', async () => {
    vi.mocked(getProjects)
      .mockResolvedValueOnce([makeProject(1)]) // ilk yükleme
      .mockResolvedValueOnce([makeProject(2, { name: 'Yeni' }), makeProject(1)]) // ekledikten sonraki yenileme
    const { result } = await renderLoadedProjects()
    vi.mocked(createProject).mockResolvedValue(makeProject(2, { name: 'Yeni' }))

    // "as" kullanımı: TypeScript'in değişkeni başlangıç değeri olan null'a daraltmasını engeller
    let created = null as Project | null
    await act(async () => {
      created = await result.current.addProject('Yeni')
    })

    expect(createProject).toHaveBeenCalledWith('Yeni')
    expect(created?.id).toBe(2)
    expect(result.current.projects.map((p) => p.name)).toEqual(['Yeni', 'Proje 1'])
  })

  it('başarısız olursa null döner ve hatayı gösterir', async () => {
    vi.mocked(getProjects).mockResolvedValue([])
    const { result } = await renderLoadedProjects()
    vi.mocked(createProject).mockRejectedValue(new ApiError('Proje adı boş olamaz.', 400))

    let created = makeProject(0) as Project | null
    await act(async () => {
      created = await result.current.addProject('')
    })

    expect(created).toBeNull()
    expect(result.current.error).toBe('Proje adı boş olamaz.')
  })
})

describe('useProjects: removeProject', () => {
  it('projeyi siler, listeyi yeniler ve true döner', async () => {
    vi.mocked(getProjects)
      .mockResolvedValueOnce([makeProject(1), makeProject(2)])
      .mockResolvedValueOnce([makeProject(2)])
    const { result } = await renderLoadedProjects()
    vi.mocked(deleteProject).mockResolvedValue(undefined)

    let deleted = false
    await act(async () => {
      deleted = await result.current.removeProject(1)
    })

    expect(deleteProject).toHaveBeenCalledWith(1)
    expect(deleted).toBe(true)
    expect(result.current.projects.map((p) => p.id)).toEqual([2])
  })

  it('başarısız olursa false döner ve hatayı gösterir', async () => {
    vi.mocked(getProjects).mockResolvedValue([makeProject(1)])
    const { result } = await renderLoadedProjects()
    vi.mocked(deleteProject).mockRejectedValue(new ApiError('Kayıt bulunamadı.', 404))

    let deleted = true
    await act(async () => {
      deleted = await result.current.removeProject(1)
    })

    expect(deleted).toBe(false)
    expect(result.current.error).toBe('Kayıt bulunamadı.')
    expect(result.current.projects).toHaveLength(1)
  })
})

describe('useProjects: reload', () => {
  it('listeyi sunucudan yeniden çeker', async () => {
    vi.mocked(getProjects)
      .mockResolvedValueOnce([makeProject(1, { taskCount: 0 })])
      .mockResolvedValueOnce([makeProject(1, { taskCount: 3 })])
    const { result } = await renderLoadedProjects()

    await act(async () => {
      await result.current.reload()
    })

    expect(result.current.projects[0].taskCount).toBe(3)
  })
})
