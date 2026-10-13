import type { AuthResponse } from '../types'
import { post } from './client'

// Giriş ve kayıt token istemez (auth: false); zaten henüz token'ımız yok.

export function login(email: string, password: string) {
  return post<AuthResponse>('/auth/login', { email, password }, { auth: false })
}

export function register(email: string, password: string) {
  return post<AuthResponse>('/auth/register', { email, password }, { auth: false })
}
