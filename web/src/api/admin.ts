import { ApiError, apiFetch, readErrorCode } from './client.ts'

export type PlatformAdminRow = {
  userId: string
  email: string
}

export type TenantRow = {
  id: string
  name: string
}

export type MembershipRow = {
  userId: string
  email: string
  tenantId: string
  tenantName: string
  role: string
}

const errorText: Record<string, string> = {
  email_in_use: 'That email is already in use.',
  invalid_password: 'The password does not meet the rules. Use 8 characters with upper, lower, a digit, and a symbol.',
  invalid_user: 'Enter the required account fields.',
  invalid_tenant: 'Enter a tenant name.',
  tenant_name_in_use: 'A tenant with that name already exists.',
  invalid_role: 'Choose a role.',
  last_platform_admin: 'The last platform admin cannot be removed.',
  request_failed: 'The API did not respond.',
}

export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    return errorText[error.code] ?? error.code
  }
  return 'The API did not respond.'
}

async function readJson<T>(path: string, token: string): Promise<T> {
  const response = await apiFetch(path, { token })
  if (!response.ok) {
    throw new ApiError(response.status, await readErrorCode(response))
  }
  return (await response.json()) as T
}

async function send(path: string, token: string, method: string, body?: unknown): Promise<void> {
  const response = await apiFetch(path, { method, token, body })
  if (!response.ok) {
    throw new ApiError(response.status, await readErrorCode(response))
  }
}

export function listPlatformAdmins(token: string) {
  return readJson<PlatformAdminRow[]>('/platform-admins', token)
}

export function createPlatformAdmin(token: string, email: string, password: string) {
  return send('/platform-admins', token, 'POST', { email, password })
}

export function updatePlatformAdmin(token: string, userId: string, email: string, password: string) {
  return send(`/platform-admins/${userId}`, token, 'PATCH', {
    email,
    password: password.trim() === '' ? null : password,
  })
}

export function deletePlatformAdmin(token: string, userId: string) {
  return send(`/platform-admins/${userId}`, token, 'DELETE')
}

export function listTenants(token: string) {
  return readJson<TenantRow[]>('/tenants', token)
}

export function createTenant(token: string, name: string, operatorUserName: string, operatorPassword: string) {
  return send('/tenants', token, 'POST', { name, operatorUserName, operatorPassword })
}

export function renameTenant(token: string, tenantId: string, name: string) {
  return send(`/tenants/${tenantId}`, token, 'PATCH', { name })
}

export function deleteTenant(token: string, tenantId: string) {
  return send(`/tenants/${tenantId}`, token, 'DELETE')
}

export function listMemberships(token: string) {
  return readJson<MembershipRow[]>('/memberships', token)
}

export function createMember(token: string, tenantId: string, email: string, password: string, role: string) {
  return send(`/tenants/${tenantId}/users`, token, 'POST', { email, password, role })
}

export function updateMemberRole(token: string, tenantId: string, userId: string, role: string) {
  return send(`/tenants/${tenantId}/users/${userId}`, token, 'PATCH', { role })
}

export function deleteMember(token: string, tenantId: string, userId: string) {
  return send(`/tenants/${tenantId}/users/${userId}`, token, 'DELETE')
}
