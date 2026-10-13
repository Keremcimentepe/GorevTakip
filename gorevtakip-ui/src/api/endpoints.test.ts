import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createProject, deleteProject, getProject, getProjects } from './projects'
import { createTask, deleteTask, updateTaskStatus } from './tasks'

// projects.ts ve tasks.ts ince sarmalayıcılardır: doğru adrese, doğru metotla, doğru gövdeyle
// istek attıklarını doğrularız. (Backend'deki uç noktalarla birebir uyuşması gerekir.)
const fetchMock = vi.fn()

beforeEach(() => {
  fetchMock.mockReset()
  fetchMock.mockImplementation(
    async () =>
      new Response('{}', { status: 200, headers: { 'Content-Type': 'application/json' } }),
  )
  vi.stubGlobal('fetch', fetchMock)
})

afterEach(() => {
  vi.unstubAllGlobals()
})

// Son isteği okunur bir özete çevirir
function lastRequest() {
  const [url, options] = fetchMock.mock.calls.at(-1) as [string, RequestInit]
  return {
    path: new URL(url).pathname,
    method: options.method ?? 'GET',
    body: options.body ? JSON.parse(options.body as string) : undefined,
  }
}

describe('projects', () => {
  it('getProjects: GET /projects', async () => {
    await getProjects()

    expect(lastRequest()).toEqual({ path: '/projects', method: 'GET', body: undefined })
  })

  it('getProject: GET /projects/{id}', async () => {
    await getProject(5)

    expect(lastRequest().path).toBe('/projects/5')
    expect(lastRequest().method).toBe('GET')
  })

  it('createProject: ad ve açıklamayı POST eder', async () => {
    await createProject('Web Sitesi', 'Kurumsal site')

    expect(lastRequest()).toEqual({
      path: '/projects',
      method: 'POST',
      body: { name: 'Web Sitesi', description: 'Kurumsal site' },
    })
  })

  it('createProject: açıklama verilmezse ya da boşsa null gönderir', async () => {
    await createProject('Web Sitesi')
    expect(lastRequest().body).toEqual({ name: 'Web Sitesi', description: null })

    await createProject('Web Sitesi', '')
    expect(lastRequest().body).toEqual({ name: 'Web Sitesi', description: null })
  })

  it('deleteProject: DELETE /projects/{id}', async () => {
    fetchMock.mockImplementation(async () => new Response(null, { status: 204 }))

    await deleteProject(3)

    expect(lastRequest()).toEqual({ path: '/projects/3', method: 'DELETE', body: undefined })
  })
})

describe('tasks', () => {
  it('createTask: POST /projects/{id}/tasks', async () => {
    await createTask(2, 'Rapor yaz')

    expect(lastRequest()).toEqual({
      path: '/projects/2/tasks',
      method: 'POST',
      body: { title: 'Rapor yaz', description: null },
    })
  })

  it('updateTaskStatus: PATCH /tasks/{id}/status', async () => {
    await updateTaskStatus(7, 'Done')

    expect(lastRequest()).toEqual({
      path: '/tasks/7/status',
      method: 'PATCH',
      body: { status: 'Done' },
    })
  })

  it('deleteTask: DELETE /tasks/{id}', async () => {
    fetchMock.mockImplementation(async () => new Response(null, { status: 204 }))

    await deleteTask(7)

    expect(lastRequest()).toEqual({ path: '/tasks/7', method: 'DELETE', body: undefined })
  })
})
