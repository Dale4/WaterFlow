import { useAuth } from '../auth/auth-context.ts'

function display(value: string | null | undefined) {
  return value && value.length > 0 ? value : '—'
}

export function ProfilePage() {
  const { profile, restoreError } = useAuth()
  const role = profile?.platformRole ?? profile?.role ?? null

  return (
    <main className="panel">
      <h1>Profile</h1>
      <p className="lede">This is the account for the current sign-in.</p>
      {restoreError ? (
        <p className="error" role="alert">
          {restoreError}
        </p>
      ) : null}
      <dl>
        <div>
          <dt>Email</dt>
          <dd>{display(profile?.email)}</dd>
        </div>
        <div>
          <dt>Role</dt>
          <dd>{display(role)}</dd>
        </div>
        <div>
          <dt>Tenant</dt>
          <dd>{display(profile?.tenantName)}</dd>
        </div>
      </dl>
    </main>
  )
}
