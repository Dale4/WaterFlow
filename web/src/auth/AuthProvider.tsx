import { useEffect, useState, type ReactNode } from 'react'
import { ApiError, apiFetch, readErrorCode, type LoginResponse, type MeResponse } from '../api/client.ts'
import { AuthContext, type MeResult } from './auth-context.ts'
import { clearSession, loadSession, saveSession } from './session.ts'

async function fetchMe(token: string): Promise<MeResult> {
  let response: Response
  try {
    response = await apiFetch('/auth/me', { token })
  } catch {
    return 'failed'
  }

  if (response.status === 401) {
    return 'unauthorized'
  }
  if (!response.ok) {
    return 'failed'
  }

  return (await response.json()) as MeResponse
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<'restoring' | 'anonymous' | 'signed-in'>(() =>
    loadSession() ? 'restoring' : 'anonymous',
  )
  const [session, setSession] = useState<LoginResponse | null>(null)
  const [profile, setProfile] = useState<MeResponse | null>(null)
  const [restoreError, setRestoreError] = useState<string | null>(null)

  useEffect(() => {
    let active = true
    const stored = loadSession()
    if (!stored) {
      return
    }

    void (async () => {
      const result = await fetchMe(stored.token)
      if (!active) {
        return
      }
      if (result === 'unauthorized') {
        clearSession()
        setSession(null)
        setProfile(null)
        setStatus('anonymous')
        return
      }
      setSession(stored)
      setProfile(result === 'failed' ? null : result)
      setRestoreError(result === 'failed' ? 'The API did not respond.' : null)
      setStatus('signed-in')
    })()

    return () => {
      active = false
    }
  }, [])

  async function login(email: string, password: string, tenantId: string) {
    const response = await apiFetch('/auth/login', {
      method: 'POST',
      body: {
        email,
        password,
        tenantId: tenantId.trim() === '' ? null : tenantId.trim(),
      },
    })

    if (!response.ok) {
      throw new ApiError(response.status, await readErrorCode(response))
    }

    const nextSession = (await response.json()) as LoginResponse
    const me = await fetchMe(nextSession.token)
    if (me === 'unauthorized') {
      throw new ApiError(401, 'invalid_credentials')
    }
    if (me === 'failed') {
      throw new ApiError(0, 'request_failed')
    }

    saveSession(nextSession)
    setSession(nextSession)
    setProfile(me)
    setRestoreError(null)
    setStatus('signed-in')
  }

  function logout() {
    clearSession()
    setSession(null)
    setProfile(null)
    setRestoreError(null)
    setStatus('anonymous')
  }

  return (
    <AuthContext.Provider value={{ status, session, profile, restoreError, login, logout }}>
      {children}
    </AuthContext.Provider>
  )
}
