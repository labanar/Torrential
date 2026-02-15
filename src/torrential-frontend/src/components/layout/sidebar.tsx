import { useNavigate, useLocation } from 'react-router-dom'
import { Download, Settings, Sun, Moon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { useTheme } from '@/components/theme-provider'

export function Sidebar() {
  const { theme, setTheme } = useTheme()
  const navigate = useNavigate()
  const location = useLocation()

  return (
    <div className="flex h-full w-48 flex-col p-4">
      <h1 className="mb-6 text-center text-lg font-bold tracking-widest text-foreground/80">
        TORRENTIAL
      </h1>

      <nav className="flex flex-col gap-1">
        <Button
          variant="ghost"
          className={`justify-start gap-2 ${location.pathname === '/' ? 'bg-accent' : ''}`}
          onClick={() => navigate('/')}
        >
          <Download className="h-4 w-4" />
          Torrents
        </Button>
        <Button
          variant="ghost"
          className={`justify-start gap-2 ${location.pathname === '/settings' ? 'bg-accent' : ''}`}
          onClick={() => navigate('/settings')}
        >
          <Settings className="h-4 w-4" />
          Settings
        </Button>
      </nav>

      <div className="mt-auto">
        <Button
          variant="ghost"
          size="icon"
          onClick={() => setTheme(theme === 'dark' ? 'light' : 'dark')}
        >
          {theme === 'dark' ? (
            <Sun className="h-5 w-5" />
          ) : (
            <Moon className="h-5 w-5" />
          )}
        </Button>
      </div>
    </div>
  )
}
