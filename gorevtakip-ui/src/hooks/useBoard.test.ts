import { act, renderHook, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../api/client'
import { getProject } from '../api/projects'
import { createTask, deleteTask, updateTaskStatus } from '../api/tasks'
import { makeProjectDetail, makeTask } from '../test/factories'
import type { ProjectDetail, Task } from '../types'
import { useBoard } from './useBoard'

// Ağ katmanını (api/ klasörünü) sahtesiyle değiştiriyoruz:
// burada sunucuyu değil, hook'un kendi mantığını sınıyoruz.
vi.mock('../api/projects')
vi.mock('../api/tasks')

const onChanged = vi.fn()

beforeEach(() => {
  vi.resetAllMocks()
})

// Hook'u çizer ve yükleme bitene kadar bekler
async function renderLoadedBoard(tasks: Task[] = [makeTask(1), makeTask(2)]) {
  vi.mocked(getProject).mockResolvedValue(makeProjectDetail(1, tasks))
  const rendered = renderHook(() => useBoard(1, onChanged))
  await waitFor(() => expect(rendered.result.current.loading).toBe(false))
  return rendered
}

function statusOf(project: ProjectDetail | null, taskId: number) {
  return project?.tasks.find((t) => t.id === taskId)?.status
}

describe('useBoard: yükleme', () => {
  it('açılışta projeyi ve görevlerini yükler', async () => {
    const { result } = await renderLoadedBoard()

    expect(getProject).toHaveBeenCalledWith(1)
    expect(result.current.project?.tasks).toHaveLength(2)
    expect(result.current.error).toBeNull()
  })

  it('yükleme sürerken loading true, proje null döner', () => {
    vi.mocked(getProject).mockReturnValue(new Promise<ProjectDetail>(() => {})) // hiç bitmeyen istek

    const { result } = renderHook(() => useBoard(1, onChanged))

    expect(result.current.loading).toBe(true)
    expect(result.current.project).toBeNull()
  })

  it('yükleme başarısız olursa hata mesajını verir ve proje null kalır', async () => {
    vi.mocked(getProject).mockRejectedValue(new ApiError('Kayıt bulunamadı.', 404))

    const { result } = renderHook(() => useBoard(99, onChanged))
    await waitFor(() => expect(result.current.loading).toBe(false))

    expect(result.current.project).toBeNull()
    expect(result.current.error).toBe('Kayıt bulunamadı.')
  })

  it('projectId değişince yeni projeyi yükler', async () => {
    vi.mocked(getProject).mockResolvedValue(makeProjectDetail(1))
    const { result, rerender } = renderHook(({ id }) => useBoard(id, onChanged), {
      initialProps: { id: 1 },
    })
    await waitFor(() => expect(result.current.loading).toBe(false))

    rerender({ id: 2 })

    await waitFor(() => expect(getProject).toHaveBeenCalledWith(2))
  })
})

describe('useBoard: addTask', () => {
  it('yeni görevi listeye ekler ve onChanged çağırır', async () => {
    const { result } = await renderLoadedBoard()
    vi.mocked(createTask).mockResolvedValue(makeTask(3, { title: 'Yeni görev' }))

    let ok = false
    await act(async () => {
      ok = await result.current.addTask('Yeni görev')
    })

    expect(ok).toBe(true)
    expect(createTask).toHaveBeenCalledWith(1, 'Yeni görev')
    expect(result.current.project?.tasks).toHaveLength(3)
    expect(result.current.project?.tasks.map((t) => t.title)).toContain('Yeni görev')
    expect(onChanged).toHaveBeenCalledTimes(1)
  })

  it('başarısız olursa false döner, hatayı gösterir, listeyi ve sayacı değiştirmez', async () => {
    const { result } = await renderLoadedBoard()
    vi.mocked(createTask).mockRejectedValue(new ApiError('Görev başlığı boş olamaz.', 400))

    let ok = true
    await act(async () => {
      ok = await result.current.addTask('x')
    })

    expect(ok).toBe(false)
    expect(result.current.error).toBe('Görev başlığı boş olamaz.')
    expect(result.current.project?.tasks).toHaveLength(2)
    expect(onChanged).not.toHaveBeenCalled()
  })
})

describe('useBoard: moveTask', () => {
  it('kartı sunucu cevabını beklemeden hemen taşır (iyimser güncelleme)', async () => {
    const { result } = await renderLoadedBoard()

    // Bitişini elle kontrol ettiğimiz, henüz tamamlanmamış bir istek
    let finishRequest: (task: Task) => void = () => {}
    vi.mocked(updateTaskStatus).mockReturnValue(
      new Promise<Task>((resolve) => {
        finishRequest = resolve
      }),
    )

    act(() => {
      void result.current.moveTask(1, 'Done')
    })

    // İstek hâlâ bitmedi ama ekrandaki durum zaten değişti
    expect(updateTaskStatus).toHaveBeenCalledWith(1, 'Done')
    expect(statusOf(result.current.project, 1)).toBe('Done')

    await act(async () => {
      finishRequest(makeTask(1, { status: 'Done' }))
    })

    expect(statusOf(result.current.project, 1)).toBe('Done')
    expect(result.current.error).toBeNull()
  })

  it('sunucu hata verirse kartı eski yerine geri döndürür ve hatayı gösterir', async () => {
    const { result } = await renderLoadedBoard()
    vi.mocked(updateTaskStatus).mockRejectedValue(new ApiError('Beklenmeyen bir hata oluştu.', 500))

    await act(async () => {
      await result.current.moveTask(1, 'Done')
    })

    expect(statusOf(result.current.project, 1)).toBe('Todo') // geri alındı
    expect(statusOf(result.current.project, 2)).toBe('Todo') // diğer kart etkilenmedi
    expect(result.current.error).toBe('Beklenmeyen bir hata oluştu.')
  })

  it('kart zaten o kolondaysa sunucuya istek atmaz', async () => {
    const { result } = await renderLoadedBoard()

    await act(async () => {
      await result.current.moveTask(1, 'Todo')
    })

    expect(updateTaskStatus).not.toHaveBeenCalled()
  })

  it('olmayan bir kart için sunucuya istek atmaz', async () => {
    const { result } = await renderLoadedBoard()

    await act(async () => {
      await result.current.moveTask(999, 'Done')
    })

    expect(updateTaskStatus).not.toHaveBeenCalled()
  })
})

describe('useBoard: removeTask', () => {
  it('görevi listeden çıkarır ve onChanged çağırır', async () => {
    const { result } = await renderLoadedBoard()
    vi.mocked(deleteTask).mockResolvedValue(undefined)

    await act(async () => {
      await result.current.removeTask(1)
    })

    expect(deleteTask).toHaveBeenCalledWith(1)
    expect(result.current.project?.tasks.map((t) => t.id)).toEqual([2])
    expect(onChanged).toHaveBeenCalledTimes(1)
  })

  it('başarısız olursa görev listede kalır ve hata gösterilir', async () => {
    const { result } = await renderLoadedBoard()
    vi.mocked(deleteTask).mockRejectedValue(new ApiError('Kayıt bulunamadı.', 404))

    await act(async () => {
      await result.current.removeTask(1)
    })

    expect(result.current.project?.tasks).toHaveLength(2)
    expect(result.current.error).toBe('Kayıt bulunamadı.')
    expect(onChanged).not.toHaveBeenCalled()
  })
})
