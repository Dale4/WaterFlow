import { useState, type FormEvent } from 'react'
import { ApiError, apiFetch, readErrorCode } from '../api/client.ts'
import { useAuth } from '../auth/auth-context.ts'

export function SettingsPage() {
  const { session } = useAuth()
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [saved, setSaved] = useState(false)
  const [pending, setPending] = useState(false)

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setPending(true)
    setError(null)
    setSaved(false)
    try {
      const response = await apiFetch('/auth/change-password', {
        method: 'POST',
        token: session?.token,
        body: { currentPassword, newPassword },
      })
      if (!response.ok) {
        const code = await readErrorCode(response)
        throw new ApiError(response.status, code)
      }
      setCurrentPassword('')
      setNewPassword('')
      setSaved(true)
    } catch (caught) {
      if (caught instanceof ApiError && caught.code === 'password_change_failed') {
        setError('password_change_failed. The current password did not match, or the new password is too weak.')
      } else {
        setError('The API did not respond.')
      }
    } finally {
      setPending(false)
    }
  }

  return (
    <main className="panel">
      <h1>Settings</h1>
      <p className="lede">Change the password for this sign-in.</p>
      <form onSubmit={onSubmit}>
        <label>
          Current password
          <input
            type="password"
            name="currentPassword"
            autoComplete="current-password"
            value={currentPassword}
            required
            onChange={(event) => setCurrentPassword(event.target.value)}
          />
        </label>
        <label>
          New password
          <input
            type="password"
            name="newPassword"
            autoComplete="new-password"
            value={newPassword}
            required
            onChange={(event) => setNewPassword(event.target.value)}
          />
        </label>
        {error ? (
          <p className="error" role="alert">
            {error}
          </p>
        ) : null}
        {saved ? <p className="hint">Password changed.</p> : null}
        <button type="submit" disabled={pending}>
          {pending ? 'Saving…' : 'Change password'}
        </button>
      </form>
    </main>
  )
}
