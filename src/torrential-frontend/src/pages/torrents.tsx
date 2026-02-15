import { useCallback, useEffect, useState } from 'react'
import {
  Plus,
  Play,
  Pause,
  Trash2,
  ArrowDownCircle,
  PauseCircle,
  CheckCircle2,
  PlusCircle,
  AlertCircle,
} from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Progress } from '@/components/ui/progress'
import { Badge } from '@/components/ui/badge'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { listTorrents, startTorrent, stopTorrent, removeTorrent } from '@/lib/api'
import type { TorrentState } from '@/lib/api'

function formatBytes(bytes: number): string {
  if (bytes === 0) return '0 B'
  const sizes = ['B', 'KB', 'MB', 'GB', 'TB']
  const i = Math.floor(Math.log(bytes) / Math.log(1024))
  if (i === 0) return bytes + ' B'
  return (bytes / Math.pow(1024, i)).toFixed(1) + ' ' + sizes[i]
}

const statusConfig = {
  Downloading: { icon: ArrowDownCircle, badgeClass: 'bg-green-600 text-white', label: 'Downloading' },
  Stopped: { icon: PauseCircle, badgeClass: 'bg-yellow-600 text-white', label: 'Stopped' },
  Completed: { icon: CheckCircle2, badgeClass: 'bg-blue-600 text-white', label: 'Completed' },
  Added: { icon: PlusCircle, badgeClass: 'bg-gray-500 text-white', label: 'Added' },
  Error: { icon: AlertCircle, badgeClass: 'bg-red-600 text-white', label: 'Error' },
} as const

export function TorrentsPage() {
  const [torrents, setTorrents] = useState<TorrentState[]>([])
  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false)
  const [deleteFiles, setDeleteFiles] = useState(false)

  const fetchTorrents = useCallback(async () => {
    try {
      const data = await listTorrents()
      setTorrents(data)
    } catch (e) {
      console.error('Failed to fetch torrents', e)
    }
  }, [])

  useEffect(() => {
    fetchTorrents()
    const interval = setInterval(fetchTorrents, 5000)
    return () => clearInterval(interval)
  }, [fetchTorrents])

  const toggleSelect = (infoHash: string) => {
    setSelected((prev) => {
      const next = new Set(prev)
      if (next.has(infoHash)) {
        next.delete(infoHash)
      } else {
        next.add(infoHash)
      }
      return next
    })
  }

  const hasSelection = selected.size > 0

  const handleStart = async () => {
    await Promise.all([...selected].map((h) => startTorrent(h)))
    fetchTorrents()
  }

  const handleStop = async () => {
    await Promise.all([...selected].map((h) => stopTorrent(h)))
    fetchTorrents()
  }

  const handleDelete = async () => {
    await Promise.all([...selected].map((h) => removeTorrent(h, deleteFiles)))
    setSelected(new Set())
    setDeleteDialogOpen(false)
    setDeleteFiles(false)
    fetchTorrents()
  }

  const selectedTorrentNames = torrents
    .filter((t) => selected.has(t.infoHash))
    .map((t) => t.name)

  if (torrents.length === 0) {
    return (
      <div className="flex flex-1 flex-col items-center justify-center gap-4 p-6">
        <p className="text-muted-foreground text-lg">No torrents added yet</p>
        <Button onClick={() => console.log('Add torrent clicked')}>
          <Plus />
          Add Torrent
        </Button>
      </div>
    )
  }

  return (
    <div className="flex flex-1 flex-col p-6 gap-4">
      {/* Action toolbar */}
      <div className="flex items-center gap-2">
        <Button
          variant="outline"
          size="icon"
          onClick={() => console.log('Add torrent clicked')}
        >
          <Plus />
        </Button>
        <Button
          variant="outline"
          size="icon"
          disabled={!hasSelection}
          onClick={handleStart}
        >
          <Play />
        </Button>
        <Button
          variant="outline"
          size="icon"
          disabled={!hasSelection}
          onClick={handleStop}
        >
          <Pause />
        </Button>
        <Button
          variant="outline"
          size="icon"
          disabled={!hasSelection}
          onClick={() => setDeleteDialogOpen(true)}
        >
          <Trash2 />
        </Button>
      </div>

      {/* Torrent list */}
      <div className="flex flex-col gap-2">
        {torrents.map((t) => {
          const config = statusConfig[t.status]
          const StatusIcon = config.icon
          // The API doesn't return progress/downloaded bytes yet, so we show totalSize
          // Once the API adds progress, this can be updated
          const progressPercent = 0
          const downloadedBytes = 0

          return (
            <div
              key={t.infoHash}
              className="flex items-center gap-3 rounded-lg border p-3"
            >
              <Checkbox
                checked={selected.has(t.infoHash)}
                onCheckedChange={() => toggleSelect(t.infoHash)}
              />
              <StatusIcon className="size-5 shrink-0 text-muted-foreground" />
              <div className="flex flex-1 flex-col gap-1.5 min-w-0">
                <div className="flex items-center justify-between gap-2">
                  <span className="truncate font-medium text-sm">{t.name}</span>
                  <Badge className={config.badgeClass}>{config.label}</Badge>
                </div>
                <Progress value={progressPercent} className="h-2" />
                <span className="text-muted-foreground text-xs">
                  {formatBytes(downloadedBytes)} of {formatBytes(t.totalSize)} ({progressPercent.toFixed(1)}%)
                </span>
              </div>
            </div>
          )
        })}
      </div>

      {/* Delete confirmation dialog */}
      <Dialog open={deleteDialogOpen} onOpenChange={setDeleteDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              Remove {selectedTorrentNames.length === 1 ? 'Torrent' : 'Torrents'}
            </DialogTitle>
            <DialogDescription>
              Are you sure you want to remove the following?
            </DialogDescription>
          </DialogHeader>
          <ul className="list-disc pl-6 text-sm">
            {selectedTorrentNames.map((name) => (
              <li key={name}>{name}</li>
            ))}
          </ul>
          <div className="flex items-center gap-2">
            <Checkbox
              id="delete-files"
              checked={deleteFiles}
              onCheckedChange={(checked) => setDeleteFiles(checked === true)}
            />
            <label htmlFor="delete-files" className="text-sm cursor-pointer">
              Delete downloaded files
            </label>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeleteDialogOpen(false)}>
              Cancel
            </Button>
            <Button variant="destructive" onClick={handleDelete}>
              Remove
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}
