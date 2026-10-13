// Backend'deki C# DTO'larının (Dtos/ klasörü) TypeScript karşılıkları.
// API JSON'u camelCase döner ve enum'ları metin olarak gönderir ("Todo", "Done"...).

export type TaskStatus = 'Todo' | 'InProgress' | 'Done'

export interface Task {
  id: number
  title: string
  description: string | null
  status: TaskStatus
  createdAt: string
  projectId: number
}

// GET /projects listesindeki kayıt (görev sayısıyla birlikte)
export interface Project {
  id: number
  name: string
  description: string | null
  createdAt: string
  taskCount: number
}

// GET /projects/{id}: proje + görevleri
export interface ProjectDetail {
  id: number
  name: string
  description: string | null
  createdAt: string
  tasks: Task[]
}

// Panodaki kolonlar, soldan sağa sırayla. Kartların ← → butonları bu sıraya göre çalışır.
export const COLUMNS: { status: TaskStatus; title: string }[] = [
  { status: 'Todo', title: 'Yapılacak' },
  { status: 'InProgress', title: 'Devam ediyor' },
  { status: 'Done', title: 'Tamamlandı' },
]

// POST /auth/login ve /auth/register cevabı (backend: AuthResponse)
export interface AuthResponse {
  token: string
  expiresAt: string // ISO tarih, UTC
  email: string
}
