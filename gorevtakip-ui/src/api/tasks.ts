import type { Task, TaskStatus } from '../types'
import { del, patch, post } from './client'

export function createTask(projectId: number, title: string, description?: string) {
  return post<Task>(`/projects/${projectId}/tasks`, { title, description: description || null })
}

// Kartı başka kolona taşımak: backend'deki PATCH /tasks/{id}/status
export function updateTaskStatus(id: number, status: TaskStatus) {
  return patch<Task>(`/tasks/${id}/status`, { status })
}

export function deleteTask(id: number) {
  return del(`/tasks/${id}`)
}
