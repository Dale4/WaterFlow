import type { ReactNode } from 'react'
import { TopBar } from './TopBar.tsx'

export function Shell({ children }: { children: ReactNode }) {
  return (
    <div className="app-shell">
      <TopBar />
      <div className="app-content">{children}</div>
    </div>
  )
}
