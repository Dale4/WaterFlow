import { createContext, use } from 'react'
import type { LoginResponse, MeResponse } from '../api/client.ts'

export type MeResult = MeResponse | 'unauthorized' | 'failed'

export type AuthContextValue = {
  status: 'restoring' | 'anonymous' | 'signed-in'
  session: LoginResponse | null
  profile: MeResponse | null
  restoreError: string | null
  login: (email: string, password: string, tenantId: string) => Promise<void>
  logout: () => void
}

export const AuthContext = createContext<AuthContextValue | null>(null)

export function useAuth(): AuthContextValue {
  const value = use(AuthContext)
  if (!value) {
    throw new Error('useAuth must be used within AuthProvider')
  }
  return value
}
