const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL || '/api').replace(/\/$/, '')

export type LoginResponse = {
  token: string
  expiresAt: string
  tenantId: string | null
  role: string | null
  platformRole: string | null
}

export type MeResponse = {
  userId: string | null
  email: string | null
  tenantId: string | null
  tenantName: string | null
  role: string | null
  platformRole: string | null
}

export class ApiError extends Error {
  readonly status: number
  readonly code: string

  constructor(status: number, code: string) {
    super(code)
    this.name = 'ApiError'
    this.status = status
    this.code = code
  }
}

type RequestOptions = {
  method?: string
  body?: unknown
  token?: string | null
}

export function apiFetch(path: string, options: RequestOptions = {}): Promise<Response> {
  const headers = new Headers()
  if (options.body !== undefined) {
    headers.set('Content-Type', 'application/json')
  }
  if (options.token) {
    headers.set('Authorization', `Bearer ${options.token}`)
  }

  return fetch(`${apiBaseUrl}${path}`, {
    method: options.method ?? 'GET',
    headers,
    body: options.body === undefined ? undefined : JSON.stringify(options.body),
  })
}

export async function readErrorCode(response: Response): Promise<string> {
  try {
    const body = (await response.json()) as { error?: string }
    if (body.error) {
      return body.error
    }
  } catch {
    // The API sometimes returns an empty unauthorized body.
  }
  return 'request_failed'
}
