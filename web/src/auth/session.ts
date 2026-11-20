import type { LoginResponse } from '../api/client.ts'

const storageKey = 'waterflow.session'

export function loadSession(): LoginResponse | null {
  const raw = sessionStorage.getItem(storageKey)
  if (!raw) {
    return null
  }

  try {
    const parsed = JSON.parse(raw) as Partial<LoginResponse>
    if (typeof parsed.token !== 'string' || parsed.token.length === 0) {
      return null
    }
    return {
      token: parsed.token,
      expiresAt: typeof parsed.expiresAt === 'string' ? parsed.expiresAt : '',
      tenantId: typeof parsed.tenantId === 'string' ? parsed.tenantId : null,
      role: typeof parsed.role === 'string' ? parsed.role : null,
      platformRole: typeof parsed.platformRole === 'string' ? parsed.platformRole : null,
    }
  } catch {
    return null
  }
}

export function saveSession(session: LoginResponse) {
  sessionStorage.setItem(storageKey, JSON.stringify(session))
}

export function clearSession() {
  sessionStorage.removeItem(storageKey)
}
