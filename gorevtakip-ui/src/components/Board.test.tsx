import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../api/client'
import { getProject } from '../api/projects'
import { createTask, updateTaskStatus } from '../api/tasks'
import { makeProjectDetail, makeTask } from '../test/factories'
import type { ProjectDetail, Task } from '../types'
import { Board } from './Board'

// Entegrasyon seviyesi: Board + useBoard + Column + TaskCard + AddForm birlikte çalışır.
// Yalnızca ağ katmanı (api/) sahte; gerisi gerçek.
vi.mock('../api/projects')
vi.mock('../api/tasks')

beforeEach(() => {
  vi.resetAllMocks()
})

const column = (name: string) => screen.getByRole('region', { name })

async function renderBoard(tasks: Task[] = []) {
  vi.mocked(getProject).mockResolvedValue(
    makeProjectDetail(1, tasks, { name: 'Web Sitesi', description: 'Kurumsal site' }),
  )
  const onTasksChanged = vi.fn()
  render(<Board projectId={1} onTasksChanged={onTasksChanged} />)
  await screen.findByRole('heading', { name: 'Web Sitesi', level: 1 }) // yükleme bitti
  return { user: userEvent.setup(), onTasksChanged }
}

describe('Board', () => {
  it('yüklenirken "Pano yükleniyor…" yazar', () => {
    vi.mocked(getProject).mockReturnValue(new Promise<ProjectDetail>(() => {}))

    render(<Board projectId={1} onTasksChanged={vi.fn()} />)

    expect(screen.getByText('Pano yükleniyor…')).toBeInTheDocument()
  })

  it('proje adını, açıklamasını ve üç kolonu çizer', async () => {
    await renderBoard()

    expect(screen.getByText('Kurumsal site')).toBeInTheDocument()
    expect(screen.getAllByRole('region').map((r) => r.getAttribute('aria-label'))).toEqual([
      'Yapılacak',
      'Devam ediyor',
      'Tamamlandı',
    ])
  })

  it('görevleri durumlarına göre kendi kolonlarına dizer', async () => {
    await renderBoard([
      makeTask(1, { title: 'Tasarımı çiz', status: 'Todo' }),
      makeTask(2, { title: 'API bağla', status: 'InProgress' }),
      makeTask(3, { title: 'Domain al', status: 'Done' }),
    ])

    expect(within(column('Yapılacak')).getByText('Tasarımı çiz')).toBeInTheDocument()
    expect(within(column('Devam ediyor')).getByText('API bağla')).toBeInTheDocument()
    expect(within(column('Tamamlandı')).getByText('Domain al')).toBeInTheDocument()
  })

  it('proje yüklenemezse panoyu çizmez, hatayı gösterir', async () => {
    vi.mocked(getProject).mockRejectedValue(new ApiError('Kayıt bulunamadı.', 404))

    render(<Board projectId={99} onTasksChanged={vi.fn()} />)

    expect(await screen.findByRole('alert')).toHaveTextContent('Kayıt bulunamadı.')
    expect(screen.queryAllByRole('region')).toHaveLength(0)
  })

  it('görev ekler: "Yapılacak" kolonunda görünür, kutu temizlenir, onTasksChanged çağrılır', async () => {
    const { user, onTasksChanged } = await renderBoard()
    vi.mocked(createTask).mockResolvedValue(makeTask(10, { title: 'Yeni görev' }))

    await user.type(screen.getByLabelText('Yeni görev başlığı'), 'Yeni görev')
    await user.click(screen.getByRole('button', { name: 'Görev ekle' }))

    expect(await within(column('Yapılacak')).findByText('Yeni görev')).toBeInTheDocument()
    expect(createTask).toHaveBeenCalledWith(1, 'Yeni görev')
    expect(onTasksChanged).toHaveBeenCalledTimes(1)
    await waitFor(() => expect(screen.getByLabelText('Yeni görev başlığı')).toHaveValue(''))
  })

  it('→ butonu kartı bir sonraki kolona taşır', async () => {
    const { user } = await renderBoard([makeTask(1, { title: 'Tasarımı çiz', status: 'Todo' })])
    vi.mocked(updateTaskStatus).mockResolvedValue(makeTask(1, { title: 'Tasarımı çiz', status: 'InProgress' }))

    await user.click(
      within(column('Yapılacak')).getByRole('button', { name: '"Devam ediyor" kolonuna taşı' }),
    )

    expect(await within(column('Devam ediyor')).findByText('Tasarımı çiz')).toBeInTheDocument()
    expect(within(column('Yapılacak')).queryByText('Tasarımı çiz')).not.toBeInTheDocument()
    expect(updateTaskStatus).toHaveBeenCalledWith(1, 'InProgress')
  })

  it('taşıma sunucuda başarısız olursa kart geri döner ve uyarı gösterilir', async () => {
    const { user } = await renderBoard([makeTask(1, { title: 'Tasarımı çiz', status: 'Todo' })])
    vi.mocked(updateTaskStatus).mockRejectedValue(new ApiError('Beklenmeyen bir hata oluştu.', 500))

    await user.click(
      within(column('Yapılacak')).getByRole('button', { name: '"Devam ediyor" kolonuna taşı' }),
    )

    expect(await screen.findByRole('alert')).toHaveTextContent('Beklenmeyen bir hata oluştu.')
    expect(within(column('Yapılacak')).getByText('Tasarımı çiz')).toBeInTheDocument()
    expect(within(column('Devam ediyor')).queryByText('Tasarımı çiz')).not.toBeInTheDocument()
  })
})
