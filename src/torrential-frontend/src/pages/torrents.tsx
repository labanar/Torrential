import { useCallback, useEffect, useRef, useState } from 'react'
import {
  Plus,
  Play,
  Pause,
  Trash2,
  ArrowDownCircle,
  PauseCircle,
  Circle,
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
import { listTorrents, parseTorrent, addTorrentWithSelections, startTorrent, stopTorrent, removeTorrent } from '@/lib/api'
import type { TorrentState, ParsedTorrent } from '@/lib/api'

function formatBytes(bytes: number): string {
  if (bytes === 0) return '0 B'
  const sizes = ['B', 'KB', 'MB', 'GB', 'TB']
  const i = Math.floor(Math.log(bytes) / Math.log(1024))
  if (i === 0) return bytes + ' B'
  return (bytes / Math.pow(1024, i)).toFixed(1) + ' ' + sizes[i]
}

const statusConfig = {
  Running: { icon: ArrowDownCircle, badgeClass: 'bg-green-600 text-white', label: 'Running' },
  Stopped: { icon: PauseCircle, badgeClass: 'bg-yellow-600 text-white', label: 'Stopped' },
  Idle: { icon: Circle, badgeClass: 'bg-gray-500 text-white', label: 'Idle' },
} as const

export function TorrentsPage() {
  const [torrents, setTorrents] = useState<TorrentState[]>([])
  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false)
  const [deleteFiles, setDeleteFiles] = useState(false)
  const fileInputRef = useRef<HTMLInputElement>(null)

  // Add torrent modal state
  const [addDialogOpen, setAddDialogOpen] = useState(false)
  const [parsedTorrent, setParsedTorrent] = useState<ParsedTorrent | null>(null)
  const [selectedFiles, setSelectedFiles] = useState<Set<number>>(new Set())
  const [addingTorrent, setAddingTorrent] = useState(false)

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

  const handleFileSelected = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file) return
    try {
      const parsed = await parseTorrent(file)
      setParsedTorrent(parsed)
      setSelectedFiles(new Set(parsed.files.map(f => f.fileIndex)))
      setAddDialogOpen(true)
    } catch (err) {
      console.error('Failed to parse torrent', err)
    }
    e.target.value = ''
  }

  const toggleFileSelect = (fileIndex: number) => {
    setSelectedFiles((prev) => {
      const next = new Set(prev)
      if (next.has(fileIndex)) {
        next.delete(fileIndex)
      } else {
        next.add(fileIndex)
      }
      return next
    })
  }

  const toggleAllFiles = () => {
    if (!parsedTorrent) return
    if (selectedFiles.size === parsedTorrent.files.length) {
      setSelectedFiles(new Set())
    } else {
      setSelectedFiles(new Set(parsedTorrent.files.map(f => f.fileIndex)))
    }
  }

  const handleConfirmAdd = async () => {
    if (!parsedTorrent) return
    setAddingTorrent(true)
    try {
      const fileSelections = parsedTorrent.files.map(f => ({
        fileIndex: f.fileIndex,
        selected: selectedFiles.has(f.fileIndex),
      }))
      await addTorrentWithSelections(parsedTorrent, fileSelections)
      setAddDialogOpen(false)
      setParsedTorrent(null)
      setSelectedFiles(new Set())
      fetchTorrents()
    } catch (err) {
      console.error('Failed to add torrent', err)
    } finally {
      setAddingTorrent(false)
    }
  }

  const handleCancelAdd = () => {
    setAddDialogOpen(false)
    setParsedTorrent(null)
    setSelectedFiles(new Set())
  }

  const selectedTorrentNames = torrents
    .filter((t) => selected.has(t.infoHash))
    .map((t) => t.name)

  const selectedTotalSize = parsedTorrent
    ? parsedTorrent.files
        .filter(f => selectedFiles.has(f.fileIndex))
        .reduce((sum, f) => sum + f.fileSize, 0)
    : 0

  if (torrents.length === 0) {
    return (
      <>
        <div className="flex flex-1 flex-col items-center justify-center gap-4 p-6">
          <p className="text-muted-foreground text-lg">No torrents added yet</p>
          <input ref={fileInputRef} type="file" accept=".torrent" className="hidden" onChange={handleFileSelected} />
          <Button onClick={() => fileInputRef.current?.click()}>
            <Plus />
            Add Torrent
          </Button>
        </div>

        {/* Add torrent file selection dialog */}
        <Dialog open={addDialogOpen} onOpenChange={(open) => { if (!open) handleCancelAdd() }}>
          <DialogContent className="sm:max-w-lg">
            <DialogHeader>
              <DialogTitle>Add Torrent</DialogTitle>
              <DialogDescription>
                {parsedTorrent?.name}
              </DialogDescription>
            </DialogHeader>
            {parsedTorrent && (
              <>
                <div className="flex items-center justify-between text-sm text-muted-foreground">
                  <span>Select files to download</span>
                  <span>{formatBytes(selectedTotalSize)} of {formatBytes(parsedTorrent.totalSize)}</span>
                </div>
                <div className="flex items-center gap-2 border-b pb-2">
                  <Checkbox
                    checked={selectedFiles.size === parsedTorrent.files.length}
                    onCheckedChange={toggleAllFiles}
                  />
                  <label className="text-sm font-medium cursor-pointer" onClick={toggleAllFiles}>
                    Select All
                  </label>
                </div>
                <div className="max-h-64 overflow-y-auto flex flex-col gap-1">
                  {parsedTorrent.files.map((f) => (
                    <div key={f.fileIndex} className="flex items-center gap-2 py-1">
                      <Checkbox
                        checked={selectedFiles.has(f.fileIndex)}
                        onCheckedChange={() => toggleFileSelect(f.fileIndex)}
                      />
                      <span className="text-sm truncate flex-1">{f.fileName}</span>
                      <span className="text-xs text-muted-foreground shrink-0">{formatBytes(f.fileSize)}</span>
                    </div>
                  ))}
                </div>
              </>
            )}
            <DialogFooter>
              <Button variant="outline" onClick={handleCancelAdd}>
                Cancel
              </Button>
              <Button onClick={handleConfirmAdd} disabled={selectedFiles.size === 0 || addingTorrent}>
                Add
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </>
    )
  }

  return (
    <div className="flex flex-1 flex-col p-6 gap-4">
      <input ref={fileInputRef} type="file" accept=".torrent" className="hidden" onChange={handleFileSelected} />

      {/* Action toolbar */}
      <div className="flex items-center gap-2">
        <Button
          variant="outline"
          size="icon"
          onClick={() => fileInputRef.current?.click()}
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
          const config = statusConfig[t.status] ?? statusConfig.Idle
          const StatusIcon = config.icon
          const progressPercent = t.progress * 100
          const downloadedBytes = t.bytesDownloaded

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
                  {formatBytes(downloadedBytes)} of {formatBytes(t.totalSizeBytes)} ({progressPercent.toFixed(1)}%)
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

      {/* Add torrent file selection dialog */}
      <Dialog open={addDialogOpen} onOpenChange={(open) => { if (!open) handleCancelAdd() }}>
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>Add Torrent</DialogTitle>
            <DialogDescription>
              {parsedTorrent?.name}
            </DialogDescription>
          </DialogHeader>
          {parsedTorrent && (
            <>
              <div className="flex items-center justify-between text-sm text-muted-foreground">
                <span>Select files to download</span>
                <span>{formatBytes(selectedTotalSize)} of {formatBytes(parsedTorrent.totalSize)}</span>
              </div>
              <div className="flex items-center gap-2 border-b pb-2">
                <Checkbox
                  checked={selectedFiles.size === parsedTorrent.files.length}
                  onCheckedChange={toggleAllFiles}
                />
                <label className="text-sm font-medium cursor-pointer" onClick={toggleAllFiles}>
                  Select All
                </label>
              </div>
              <div className="max-h-64 overflow-y-auto flex flex-col gap-1">
                {parsedTorrent.files.map((f) => (
                  <div key={f.fileIndex} className="flex items-center gap-2 py-1">
                    <Checkbox
                      checked={selectedFiles.has(f.fileIndex)}
                      onCheckedChange={() => toggleFileSelect(f.fileIndex)}
                    />
                    <span className="text-sm truncate flex-1">{f.fileName}</span>
                    <span className="text-xs text-muted-foreground shrink-0">{formatBytes(f.fileSize)}</span>
                  </div>
                ))}
              </div>
            </>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={handleCancelAdd}>
              Cancel
            </Button>
            <Button onClick={handleConfirmAdd} disabled={selectedFiles.size === 0 || addingTorrent}>
              Add
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}
