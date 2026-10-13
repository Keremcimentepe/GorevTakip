import type { Project, ProjectDetail, Task } from '../types'

// Test verisi üreticileri: her testte 6 alanlı nesneyi elle yazmak yerine sadece
// o testi ilgilendiren alanı belirtiriz:  makeTask(1, { status: 'Done' })

export function makeTask(id: number, overrides: Partial<Task> = {}): Task {
  return {
    id,
    title: `Görev ${id}`,
    description: null,
    status: 'Todo',
    createdAt: '2026-01-01T00:00:00Z',
    projectId: 1,
    ...overrides,
  }
}

export function makeProject(id: number, overrides: Partial<Project> = {}): Project {
  return {
    id,
    name: `Proje ${id}`,
    description: null,
    createdAt: '2026-01-01T00:00:00Z',
    taskCount: 0,
    ...overrides,
  }
}

export function makeProjectDetail(
  id: number,
  tasks: Task[] = [],
  overrides: Partial<ProjectDetail> = {},
): ProjectDetail {
  return {
    id,
    name: `Proje ${id}`,
    description: null,
    createdAt: '2026-01-01T00:00:00Z',
    tasks,
    ...overrides,
  }
}
