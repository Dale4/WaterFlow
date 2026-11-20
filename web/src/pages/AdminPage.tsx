import { useEffect, useState, type FormEvent, type ReactNode } from 'react'
import {
  createMember,
  createPlatformAdmin,
  createTenant,
  deleteMember,
  deletePlatformAdmin,
  deleteTenant,
  errorMessage,
  listMemberships,
  listPlatformAdmins,
  listTenants,
  renameTenant,
  updateMemberRole,
  updatePlatformAdmin,
  type MembershipRow,
  type PlatformAdminRow,
  type TenantRow,
} from '../api/admin.ts'
import { useAuth } from '../auth/auth-context.ts'

type Tab = 'platform' | 'tenants' | 'users'

const roles = ['Operator', 'BillingClerk', 'Board', 'Homeowner']

type Editor =
  | { kind: 'add-platform' }
  | { kind: 'edit-platform'; row: PlatformAdminRow }
  | { kind: 'delete-platform'; row: PlatformAdminRow }
  | { kind: 'add-tenant' }
  | { kind: 'edit-tenant'; row: TenantRow }
  | { kind: 'delete-tenant'; row: TenantRow }
  | { kind: 'add-user' }
  | { kind: 'edit-user'; row: MembershipRow }
  | { kind: 'delete-user'; row: MembershipRow }

export function AdminPage() {
  const { session } = useAuth()
  const token = session?.token ?? ''
  const [tab, setTab] = useState<Tab>('platform')
  const [search, setSearch] = useState('')
  const [platformAdmins, setPlatformAdmins] = useState<PlatformAdminRow[]>([])
  const [tenants, setTenants] = useState<TenantRow[]>([])
  const [memberships, setMemberships] = useState<MembershipRow[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [editor, setEditor] = useState<Editor | null>(null)
  const [reloadKey, setReloadKey] = useState(0)

  useEffect(() => {
    let active = true
    setLoading(true)
    setError(null)

    void (async () => {
      try {
        if (tab === 'platform') {
          const rows = await listPlatformAdmins(token)
          if (active) setPlatformAdmins(rows)
        } else if (tab === 'tenants') {
          const rows = await listTenants(token)
          if (active) setTenants(rows)
        } else {
          const [members, tenantRows] = await Promise.all([listMemberships(token), listTenants(token)])
          if (active) {
            setMemberships(members)
            setTenants(tenantRows)
          }
        }
      } catch (caught) {
        if (active) setError(errorMessage(caught))
      } finally {
        if (active) setLoading(false)
      }
    })()

    return () => {
      active = false
    }
  }, [tab, token, reloadKey])

  function saved() {
    setEditor(null)
    setReloadKey((current) => current + 1)
  }

  const needle = search.trim().toLowerCase()
  const visibleAdmins = platformAdmins.filter((row) => needle === '' || row.email.toLowerCase().includes(needle))
  const visibleTenants = tenants.filter((row) => needle === '' || row.name.toLowerCase().includes(needle))
  const visibleMembers = memberships.filter((row) => {
    if (needle === '') return true
    return [row.email, row.tenantName, row.role].some((value) => value.toLowerCase().includes(needle))
  })

  return (
    <section className="page">
      <h1>Administration</h1>
      <p className="lede">Platform admins, water systems, and the people who belong to them.</p>
      <div className="tabs" role="tablist" aria-label="Administration">
        <TabButton current={tab} id="platform" onSelect={setTab}>
          Platform Users
        </TabButton>
        <TabButton current={tab} id="tenants" onSelect={setTab}>
          Tenants
        </TabButton>
        <TabButton current={tab} id="users" onSelect={setTab}>
          Users
        </TabButton>
      </div>
      <div className="toolbar">
        <label className="search">
          Search
          <input value={search} onChange={(event) => setSearch(event.target.value)} />
        </label>
        <button type="button" onClick={() => setEditor(addKind(tab))} disabled={loading}>
          Add
        </button>
      </div>
      {error ? (
        <p className="error" role="alert">
          {error}
        </p>
      ) : null}
      {loading ? <p className="hint">Loading…</p> : null}
      {!loading && tab === 'platform' ? (
        <DataTable
          columns={['Email', 'Actions']}
          empty="No platform admins match this search."
          rows={visibleAdmins.map((row) => [
            row.email,
            <RowActions
              key={row.userId}
              onEdit={() => setEditor({ kind: 'edit-platform', row })}
              onDelete={() => setEditor({ kind: 'delete-platform', row })}
            />,
          ])}
        />
      ) : null}
      {!loading && tab === 'tenants' ? (
        <DataTable
          columns={['Name', 'Actions']}
          empty="No tenants match this search."
          rows={visibleTenants.map((row) => [
            row.name,
            <RowActions
              key={row.id}
              onEdit={() => setEditor({ kind: 'edit-tenant', row })}
              onDelete={() => setEditor({ kind: 'delete-tenant', row })}
            />,
          ])}
        />
      ) : null}
      {!loading && tab === 'users' ? (
        <DataTable
          columns={['Email', 'Tenant', 'Role', 'Actions']}
          empty="No users match this search."
          rows={visibleMembers.map((row) => [
            row.email,
            row.tenantName,
            row.role,
            <RowActions
              key={`${row.tenantId}-${row.userId}`}
              onEdit={() => setEditor({ kind: 'edit-user', row })}
              onDelete={() => setEditor({ kind: 'delete-user', row })}
            />,
          ])}
        />
      ) : null}
      {editor ? (
        <EditorDialog
          editor={editor}
          token={token}
          tenants={tenants}
          onClose={() => setEditor(null)}
          onSaved={saved}
        />
      ) : null}
    </section>
  )
}

function addKind(tab: Tab): Editor {
  if (tab === 'platform') return { kind: 'add-platform' }
  if (tab === 'tenants') return { kind: 'add-tenant' }
  return { kind: 'add-user' }
}

function TabButton({
  current,
  id,
  onSelect,
  children,
}: {
  current: Tab
  id: Tab
  onSelect: (tab: Tab) => void
  children: string
}) {
  return (
    <button type="button" role="tab" aria-selected={current === id} onClick={() => onSelect(id)}>
      {children}
    </button>
  )
}

function RowActions({ onEdit, onDelete }: { onEdit: () => void; onDelete: () => void }) {
  return (
    <div className="row-actions">
      <button type="button" className="secondary" onClick={onEdit}>
        Edit
      </button>
      <button type="button" className="danger" onClick={onDelete}>
        Delete
      </button>
    </div>
  )
}

function DataTable({ columns, rows, empty }: { columns: string[]; rows: ReactNode[][]; empty: string }) {
  if (rows.length === 0) {
    return <p className="hint">{empty}</p>
  }

  return (
    <div className="table-wrap">
      <table>
        <thead>
          <tr>
            {columns.map((column) => (
              <th key={column} scope="col">
                {column}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((cells, index) => (
            <tr key={index}>
              {cells.map((cell, cellIndex) => (
                <td key={cellIndex}>{cell}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function EditorDialog({
  editor,
  token,
  tenants,
  onClose,
  onSaved,
}: {
  editor: Editor
  token: string
  tenants: TenantRow[]
  onClose: () => void
  onSaved: () => void
}) {
  return (
    <div className="dialog-backdrop" role="presentation">
      <div className="dialog" role="dialog" aria-modal="true">
        {editor.kind === 'add-platform' ? (
          <PlatformForm
            title="Add platform admin"
            email=""
            requirePassword
            submitLabel="Add"
            onClose={onClose}
            onSubmit={(email, password) => createPlatformAdmin(token, email, password)}
            onSaved={onSaved}
          />
        ) : null}
        {editor.kind === 'edit-platform' ? (
          <PlatformForm
            title="Edit platform admin"
            email={editor.row.email}
            requirePassword={false}
            submitLabel="Save"
            onClose={onClose}
            onSubmit={(email, password) => updatePlatformAdmin(token, editor.row.userId, email, password)}
            onSaved={onSaved}
          />
        ) : null}
        {editor.kind === 'delete-platform' ? (
          <ConfirmDialog
            title="Delete platform admin"
            message={`Remove ${editor.row.email}? An account with no water-system membership is deleted.`}
            onClose={onClose}
            onConfirm={() => deletePlatformAdmin(token, editor.row.userId)}
            onSaved={onSaved}
          />
        ) : null}
        {editor.kind === 'add-tenant' ? (
          <TenantCreateForm token={token} onClose={onClose} onSaved={onSaved} />
        ) : null}
        {editor.kind === 'edit-tenant' ? (
          <TenantRenameForm tenant={editor.row} token={token} onClose={onClose} onSaved={onSaved} />
        ) : null}
        {editor.kind === 'delete-tenant' ? (
          <ConfirmDialog
            title="Delete tenant"
            message={`Delete ${editor.row.name}? Memberships in this water system are removed.`}
            onClose={onClose}
            onConfirm={() => deleteTenant(token, editor.row.id)}
            onSaved={onSaved}
          />
        ) : null}
        {editor.kind === 'add-user' ? (
          <UserCreateForm tenants={tenants} token={token} onClose={onClose} onSaved={onSaved} />
        ) : null}
        {editor.kind === 'edit-user' ? (
          <UserRoleForm membership={editor.row} token={token} onClose={onClose} onSaved={onSaved} />
        ) : null}
        {editor.kind === 'delete-user' ? (
          <ConfirmDialog
            title="Delete user"
            message={`Remove ${editor.row.email} from ${editor.row.tenantName}?`}
            onClose={onClose}
            onConfirm={() => deleteMember(token, editor.row.tenantId, editor.row.userId)}
            onSaved={onSaved}
          />
        ) : null}
      </div>
    </div>
  )
}

function PlatformForm({
  title,
  email,
  requirePassword,
  submitLabel,
  onClose,
  onSubmit,
  onSaved,
}: {
  title: string
  email: string
  requirePassword: boolean
  submitLabel: string
  onClose: () => void
  onSubmit: (email: string, password: string) => Promise<void>
  onSaved: () => void
}) {
  const [nextEmail, setNextEmail] = useState(email)
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)

  async function onFormSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setPending(true)
    setError(null)
    try {
      await onSubmit(nextEmail.trim(), password)
      onSaved()
    } catch (caught) {
      setError(errorMessage(caught))
    } finally {
      setPending(false)
    }
  }

  return (
    <form onSubmit={onFormSubmit}>
      <h2>{title}</h2>
      <label>
        Email
        <input type="email" value={nextEmail} required onChange={(event) => setNextEmail(event.target.value)} />
      </label>
      <label>
        Password
        <input
          type="password"
          value={password}
          required={requirePassword}
          autoComplete="new-password"
          onChange={(event) => setPassword(event.target.value)}
        />
      </label>
      {requirePassword ? null : <p className="hint">Leave the password blank to keep the current one.</p>}
      {error ? (
        <p className="error" role="alert">
          {error}
        </p>
      ) : null}
      <DialogButtons pending={pending} submitLabel={submitLabel} onClose={onClose} />
    </form>
  )
}

function TenantCreateForm({
  token,
  onClose,
  onSaved,
}: {
  token: string
  onClose: () => void
  onSaved: () => void
}) {
  const [name, setName] = useState('')
  const [operatorUserName, setOperatorUserName] = useState('')
  const [operatorPassword, setOperatorPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setPending(true)
    setError(null)
    try {
      await createTenant(token, name.trim(), operatorUserName.trim(), operatorPassword)
      onSaved()
    } catch (caught) {
      setError(errorMessage(caught))
    } finally {
      setPending(false)
    }
  }

  return (
    <form onSubmit={onSubmit}>
      <h2>Add tenant</h2>
      <label>
        Name
        <input value={name} required onChange={(event) => setName(event.target.value)} />
      </label>
      <label>
        Operator username
        <input
          type="email"
          value={operatorUserName}
          required
          onChange={(event) => setOperatorUserName(event.target.value)}
        />
      </label>
      <label>
        Operator password
        <input
          type="password"
          value={operatorPassword}
          required
          autoComplete="new-password"
          onChange={(event) => setOperatorPassword(event.target.value)}
        />
      </label>
      {error ? (
        <p className="error" role="alert">
          {error}
        </p>
      ) : null}
      <DialogButtons pending={pending} submitLabel="Add" onClose={onClose} />
    </form>
  )
}

function TenantRenameForm({
  tenant,
  token,
  onClose,
  onSaved,
}: {
  tenant: TenantRow
  token: string
  onClose: () => void
  onSaved: () => void
}) {
  const [name, setName] = useState(tenant.name)
  const [error, setError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setPending(true)
    setError(null)
    try {
      await renameTenant(token, tenant.id, name.trim())
      onSaved()
    } catch (caught) {
      setError(errorMessage(caught))
    } finally {
      setPending(false)
    }
  }

  return (
    <form onSubmit={onSubmit}>
      <h2>Edit tenant</h2>
      <label>
        Name
        <input value={name} required onChange={(event) => setName(event.target.value)} />
      </label>
      {error ? (
        <p className="error" role="alert">
          {error}
        </p>
      ) : null}
      <DialogButtons pending={pending} submitLabel="Save" onClose={onClose} />
    </form>
  )
}

function UserCreateForm({
  tenants,
  token,
  onClose,
  onSaved,
}: {
  tenants: TenantRow[]
  token: string
  onClose: () => void
  onSaved: () => void
}) {
  const [tenantId, setTenantId] = useState(tenants[0]?.id ?? '')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [role, setRole] = useState(roles[0])
  const [error, setError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (tenantId === '') {
      setError('Create a tenant before adding a user.')
      return
    }
    setPending(true)
    setError(null)
    try {
      await createMember(token, tenantId, email.trim(), password, role)
      onSaved()
    } catch (caught) {
      setError(errorMessage(caught))
    } finally {
      setPending(false)
    }
  }

  return (
    <form onSubmit={onSubmit}>
      <h2>Add user</h2>
      <label>
        Tenant
        <select value={tenantId} required onChange={(event) => setTenantId(event.target.value)}>
          {tenants.length === 0 ? <option value="">No tenants yet</option> : null}
          {tenants.map((tenant) => (
            <option key={tenant.id} value={tenant.id}>
              {tenant.name}
            </option>
          ))}
        </select>
      </label>
      <label>
        Email
        <input type="email" value={email} required onChange={(event) => setEmail(event.target.value)} />
      </label>
      <label>
        Password
        <input
          type="password"
          value={password}
          required
          autoComplete="new-password"
          onChange={(event) => setPassword(event.target.value)}
        />
      </label>
      <RoleField role={role} onChange={setRole} />
      {error ? (
        <p className="error" role="alert">
          {error}
        </p>
      ) : null}
      <DialogButtons pending={pending} submitLabel="Add" onClose={onClose} />
    </form>
  )
}

function UserRoleForm({
  membership,
  token,
  onClose,
  onSaved,
}: {
  membership: MembershipRow
  token: string
  onClose: () => void
  onSaved: () => void
}) {
  const [role, setRole] = useState(membership.role)
  const [error, setError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setPending(true)
    setError(null)
    try {
      await updateMemberRole(token, membership.tenantId, membership.userId, role)
      onSaved()
    } catch (caught) {
      setError(errorMessage(caught))
    } finally {
      setPending(false)
    }
  }

  return (
    <form onSubmit={onSubmit}>
      <h2>Edit user</h2>
      <p className="hint">
        {membership.email} in {membership.tenantName}
      </p>
      <RoleField role={role} onChange={setRole} />
      {error ? (
        <p className="error" role="alert">
          {error}
        </p>
      ) : null}
      <DialogButtons pending={pending} submitLabel="Save" onClose={onClose} />
    </form>
  )
}

function RoleField({ role, onChange }: { role: string; onChange: (role: string) => void }) {
  return (
    <label>
      Role
      <select value={role} onChange={(event) => onChange(event.target.value)}>
        {roles.map((item) => (
          <option key={item} value={item}>
            {item}
          </option>
        ))}
      </select>
    </label>
  )
}

function ConfirmDialog({
  title,
  message,
  onClose,
  onConfirm,
  onSaved,
}: {
  title: string
  message: string
  onClose: () => void
  onConfirm: () => Promise<void>
  onSaved: () => void
}) {
  const [error, setError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)

  async function confirm() {
    setPending(true)
    setError(null)
    try {
      await onConfirm()
      onSaved()
    } catch (caught) {
      setError(errorMessage(caught))
    } finally {
      setPending(false)
    }
  }

  return (
    <div>
      <h2>{title}</h2>
      <p>{message}</p>
      {error ? (
        <p className="error" role="alert">
          {error}
        </p>
      ) : null}
      <div className="dialog-buttons">
        <button type="button" className="secondary" onClick={onClose} disabled={pending}>
          Cancel
        </button>
        <button type="button" className="danger" onClick={() => void confirm()} disabled={pending}>
          {pending ? 'Deleting…' : 'Delete'}
        </button>
      </div>
    </div>
  )
}

function DialogButtons({
  pending,
  submitLabel,
  onClose,
}: {
  pending: boolean
  submitLabel: string
  onClose: () => void
}) {
  return (
    <div className="dialog-buttons">
      <button type="button" className="secondary" onClick={onClose} disabled={pending}>
        Cancel
      </button>
      <button type="submit" disabled={pending}>
        {pending ? 'Saving…' : submitLabel}
      </button>
    </div>
  )
}
