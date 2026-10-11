import type { Project, ProjectDetail } from '../types'
import { del, get, post } from './client'

export function getProjects() {
  return get<Project[]>('/projects')
}

export function getProject(id: number) {
  return get<ProjectDetail>(`/projects/${id}`)
}

export function createProject(name: string, description?: string) {
  return post<Project>('/projects', { name, description: description || null })
}

export function deleteProject(id: number) {
  return del(`/projects/${id}`)
}
