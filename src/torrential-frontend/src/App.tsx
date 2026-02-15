import { createBrowserRouter, RouterProvider } from 'react-router-dom'
import { ThemeProvider } from '@/components/theme-provider'
import { AppLayout } from '@/components/layout/app-layout'
import { TorrentsPage } from '@/pages/torrents'
import { SettingsPage } from '@/pages/settings'

const router = createBrowserRouter([
  {
    element: <AppLayout />,
    children: [
      { path: '/', element: <TorrentsPage /> },
      { path: '/settings', element: <SettingsPage /> },
    ],
  },
])

function App() {
  return (
    <ThemeProvider defaultTheme="dark" storageKey="torrential-theme">
      <RouterProvider router={router} />
    </ThemeProvider>
  )
}

export default App
