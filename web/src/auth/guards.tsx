import type { ReactNode } from 'react'
import { Navigate } from 'react-router'
import { useAuth } from './auth-context.ts'

export function RequireAuth({ children }: { children: ReactNode }) {
  const { status } = useAuth()
  if (status === 'restoring') {
    return <p className="lede">Checking session…</p>
  }
  if (status !== 'signed-in') {
    return <Navigate to="/login" replace />
  }
  return children
}

export function RequirePlatformAdmin({ children }: { children: ReactNode }) {
  const { status, profile } = useAuth()
  if (status === 'restoring') {
    return <p className="lede">Checking session…</p>
  }
  if (status !== 'signed-in' || profile?.platformRole !== 'PlatformAdmin') {
    return <Navigate to="/" replace />
  }
  return children
}
