import { useCallback, useEffect, useState } from 'react'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import { getSettings, updateSettings } from '@/lib/api'

export function SettingsPage() {
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [downloadFolder, setDownloadFolder] = useState('')
  const [completedFolder, setCompletedFolder] = useState('')
  const [maxHalfOpenConnections, setMaxHalfOpenConnections] = useState(0)
  const [maxPeersPerTorrent, setMaxPeersPerTorrent] = useState(0)

  const fetchSettings = useCallback(async () => {
    try {
      const data = await getSettings()
      setDownloadFolder(data.downloadFolder)
      setCompletedFolder(data.completedFolder)
      setMaxHalfOpenConnections(data.maxHalfOpenConnections)
      setMaxPeersPerTorrent(data.maxPeersPerTorrent)
    } catch (e) {
      console.error('Failed to fetch settings', e)
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    fetchSettings()
  }, [fetchSettings])

  const handleSave = async () => {
    setSaving(true)
    try {
      const updated = await updateSettings({
        downloadFolder,
        completedFolder,
        maxHalfOpenConnections,
        maxPeersPerTorrent,
      })
      setDownloadFolder(updated.downloadFolder)
      setCompletedFolder(updated.completedFolder)
      setMaxHalfOpenConnections(updated.maxHalfOpenConnections)
      setMaxPeersPerTorrent(updated.maxPeersPerTorrent)
    } catch (e) {
      console.error('Failed to save settings', e)
    } finally {
      setSaving(false)
    }
  }

  if (loading) {
    return (
      <div className="flex flex-1 items-center justify-center p-6">
        <p className="text-muted-foreground text-sm">Loading settings...</p>
      </div>
    )
  }

  return (
    <div className="flex flex-1 justify-center p-6">
      <Card className="w-full max-w-2xl h-fit">
        <CardHeader>
          <CardTitle>Settings</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-6">
          <div className="flex flex-col gap-4">
            <h3 className="text-sm font-semibold">Folders</h3>
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-medium">Download Folder</label>
              <Input
                value={downloadFolder}
                onChange={(e) => setDownloadFolder(e.target.value)}
              />
              <p className="text-muted-foreground text-xs">
                Directory where active downloads are saved
              </p>
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-medium">Completed Folder</label>
              <Input
                value={completedFolder}
                onChange={(e) => setCompletedFolder(e.target.value)}
              />
              <p className="text-muted-foreground text-xs">
                Directory where completed downloads are moved
              </p>
            </div>
          </div>

          <div className="flex flex-col gap-4">
            <h3 className="text-sm font-semibold">Connection Limits</h3>
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-medium">Max Half-Open Connections</label>
              <Input
                type="number"
                min={1}
                value={maxHalfOpenConnections}
                onChange={(e) => setMaxHalfOpenConnections(Number(e.target.value))}
              />
              <p className="text-muted-foreground text-xs">
                Maximum number of simultaneous half-open connections
              </p>
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-medium">Max Peers Per Torrent</label>
              <Input
                type="number"
                min={1}
                value={maxPeersPerTorrent}
                onChange={(e) => setMaxPeersPerTorrent(Number(e.target.value))}
              />
              <p className="text-muted-foreground text-xs">
                Maximum number of peers to connect to per torrent
              </p>
            </div>
          </div>

          <div className="flex justify-end">
            <Button onClick={handleSave} disabled={saving}>
              {saving ? 'Saving...' : 'Save'}
            </Button>
          </div>
        </CardContent>
      </Card>
    </div>
  )
}
