import { BrowserRouter, Route, Routes } from 'react-router'
import { AuthProvider } from './auth/AuthProvider.tsx'
import { RequireAuth, RequirePlatformAdmin } from './auth/guards.tsx'
import { Shell } from './layout/Shell.tsx'
import { AdminPage } from './pages/AdminPage.tsx'
import { HomePage } from './pages/HomePage.tsx'
import { LoginPage } from './pages/LoginPage.tsx'
import { ProfilePage } from './pages/ProfilePage.tsx'
import { SettingsPage } from './pages/SettingsPage.tsx'

export default function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Shell>
          <Routes>
            <Route path="/" element={<HomePage />} />
            <Route path="/login" element={<LoginPage />} />
            <Route
              path="/profile"
              element={
                <RequireAuth>
                  <ProfilePage />
                </RequireAuth>
              }
            />
            <Route
              path="/settings"
              element={
                <RequireAuth>
                  <SettingsPage />
                </RequireAuth>
              }
            />
            <Route
              path="/admin"
              element={
                <RequirePlatformAdmin>
                  <AdminPage />
                </RequirePlatformAdmin>
              }
            />
          </Routes>
        </Shell>
      </BrowserRouter>
    </AuthProvider>
  )
}
