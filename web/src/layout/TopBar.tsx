import { useEffect, useRef, useState } from 'react'
import { NavLink, useNavigate } from 'react-router'
import { useAuth } from '../auth/auth-context.ts'

export function TopBar() {
  const { status, profile, logout } = useAuth()
  const navigate = useNavigate()
  const [open, setOpen] = useState(false)
  const menuRef = useRef<HTMLDivElement>(null)
  const isAdmin = profile?.platformRole === 'PlatformAdmin'

  useEffect(() => {
    if (!open) {
      return
    }

    function onPointerDown(event: PointerEvent) {
      if (!menuRef.current?.contains(event.target as Node)) {
        setOpen(false)
      }
    }

    document.addEventListener('pointerdown', onPointerDown)
    return () => document.removeEventListener('pointerdown', onPointerDown)
  }, [open])

  function signOut() {
    setOpen(false)
    logout()
    navigate('/')
  }

  return (
    <header className="topbar">
      <NavLink to="/" className="brand" end>
        WaterFlow
      </NavLink>
      <nav className="topnav" aria-label="Primary">
        <NavLink to="/" end>
          Home
        </NavLink>
        {isAdmin ? <NavLink to="/admin">Administration</NavLink> : null}
      </nav>
      <div className="topbar-end">
        {status === 'restoring' ? null : status === 'signed-in' ? (
          <div className="user-menu" ref={menuRef}>
            <button
              type="button"
              className="account-button"
              aria-expanded={open}
              aria-haspopup="menu"
              onClick={() => setOpen((current) => !current)}
            >
              {profile?.email || 'Account'}
            </button>
            {open ? (
              <div className="menu" role="menu">
                <NavLink role="menuitem" to="/profile" onClick={() => setOpen(false)}>
                  Profile
                </NavLink>
                <NavLink role="menuitem" to="/settings" onClick={() => setOpen(false)}>
                  Settings
                </NavLink>
                <button type="button" role="menuitem" onClick={signOut}>
                  Sign out
                </button>
              </div>
            ) : null}
          </div>
        ) : (
          <NavLink className="sign-in" to="/login">
            Sign In
          </NavLink>
        )}
      </div>
    </header>
  )
}
