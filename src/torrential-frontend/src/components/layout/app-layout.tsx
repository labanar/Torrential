import { Outlet } from 'react-router-dom'
import { Sidebar } from './sidebar'
import { Separator } from '@/components/ui/separator'

export function AppLayout() {
  return (
    <div className="flex h-screen">
      <Sidebar />
      <Separator orientation="vertical" />
      <main className="flex-1 overflow-auto">
        <Outlet />
      </main>
    </div>
  )
}
