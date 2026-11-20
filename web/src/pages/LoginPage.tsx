import { useState, type FormEvent } from 'react'
import { Navigate, useNavigate } from 'react-router'
import { ApiError } from '../api/client.ts'
import { useAuth } from '../auth/auth-context.ts'

const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

const errorText: Record<string, string> = {
  invalid_credentials: 'invalid_credentials. The email, password, or tenant did not match.',
  locked_out: 'locked_out. This account is locked. Try again later.',
  tenant_required: 'tenant_required. This account belongs to more than one water system. Enter a tenant id.',
}

export function LoginPage() {
  const { status, login } = useAuth()
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [tenantId, setTenantId] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const trimmedTenant = tenantId.trim()
    if (trimmedTenant !== '' && !guidPattern.test(trimmedTenant)) {
      setError('Enter a tenant id as a GUID, or leave it blank.')
      return
    }

    setPending(true)
    setError(null)
    try {
      await login(email.trim(), password, trimmedTenant)
      navigate('/')
    } catch (caught) {
      if (caught instanceof ApiError) {
        setError(errorText[caught.code] ?? caught.code)
      } else {
        setError('The API did not respond.')
      }
    } finally {
      setPending(false)
    }
  }

  if (status === 'signed-in') {
    return <Navigate to="/" replace />
  }

  return (
    <main className="panel">
      <h1>WaterFlow</h1>
      <p className="lede">Sign in to continue.</p>
      <form onSubmit={onSubmit}>
        <label>
          Email
          <input
            type="email"
            name="email"
            autoComplete="username"
            value={email}
            required
            onChange={(event) => setEmail(event.target.value)}
          />
        </label>
        <label>
          Password
          <input
            type="password"
            name="password"
            autoComplete="current-password"
            value={password}
            required
            onChange={(event) => setPassword(event.target.value)}
          />
        </label>
        <label>
          Tenant id
          <input
            type="text"
            name="tenantId"
            spellCheck={false}
            value={tenantId}
            onChange={(event) => setTenantId(event.target.value)}
          />
        </label>
        <p className="hint">
          Leave the tenant id blank for a platform admin, or for an account with one membership. Enter it when
          signing in as a member of a specific water system.
        </p>
        {error ? (
          <p className="error" role="alert">
            {error}
          </p>
        ) : null}
        <button type="submit" disabled={pending}>
          {pending ? 'Signing in…' : 'Sign in'}
        </button>
      </form>
    </main>
  )
}
