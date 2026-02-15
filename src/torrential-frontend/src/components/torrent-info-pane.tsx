import { useCallback, useEffect, useState } from 'react'
import { getTorrentDetails } from '@/lib/api'
import type { TorrentDetails } from '@/lib/api'
import { Badge } from '@/components/ui/badge'
import { formatBytes } from '@/lib/format'
import { PieceGrid } from '@/components/piece-grid'
import { PeersList } from '@/components/peers-list'
import { FilesList } from '@/components/files-list'

const statusConfig = {
  Running: { badgeClass: 'bg-green-600 text-white', label: 'Running' },
  Stopped: { badgeClass: 'bg-yellow-600 text-white', label: 'Stopped' },
  Idle: { badgeClass: 'bg-gray-500 text-white', label: 'Idle' },
} as const

interface TorrentInfoPaneProps {
  infoHash: string
}

export function TorrentInfoPane({ infoHash }: TorrentInfoPaneProps) {
  const [details, setDetails] = useState<TorrentDetails | null>(null)
  const [activeTab, setActiveTab] = useState<'pieces' | 'peers' | 'files'>('pieces')

  const fetchDetails = useCallback(async () => {
    const data = await getTorrentDetails(infoHash)
    setDetails(data)
  }, [infoHash])

  useEffect(() => {
    let cancelled = false
    const fetch = async () => {
      const data = await getTorrentDetails(infoHash)
      if (!cancelled) setDetails(data)
    }
    fetch()
    const interval = setInterval(fetch, 5000)
    return () => { cancelled = true; clearInterval(interval) }
  }, [infoHash])

  if (!details) return <div className="p-4 text-muted-foreground">Loading...</div>

  const downloadedCount = details.pieces.filter(Boolean).length
  const progressPercent = details.numberOfPieces > 0
    ? ((downloadedCount / details.numberOfPieces) * 100).toFixed(1)
    : '0.0'
  const downloadedBytes = details.numberOfPieces > 0
    ? Math.round((downloadedCount / details.numberOfPieces) * details.totalSizeBytes)
    : 0
  const config = statusConfig[details.status] ?? statusConfig.Idle

  return (
    <div className="flex h-full flex-col overflow-hidden border-t">
      {/* Header: name + stats summary */}
      <div className="flex flex-col gap-1.5 px-4 pt-3 pb-2">
        <span className="text-sm font-medium truncate" title={details.name}>{details.name}</span>
        <div className="flex items-center gap-3 flex-wrap text-xs">
          <Badge className={config.badgeClass}>{config.label}</Badge>
          <span><span className="text-muted-foreground">Progress </span><span className="font-medium">{progressPercent}%</span></span>
          <span><span className="text-muted-foreground">Size </span><span className="font-medium">{formatBytes(downloadedBytes)} / {formatBytes(details.totalSizeBytes)}</span></span>
          <span><span className="text-muted-foreground">Pieces </span><span className="font-medium">{downloadedCount} / {details.numberOfPieces}</span></span>
          <span><span className="text-muted-foreground">Piece Size </span><span className="font-medium">{formatBytes(details.pieceSize)}</span></span>
          <span><span className="text-muted-foreground">Added </span><span className="font-medium">{new Date(details.dateAdded).toLocaleDateString()}</span></span>
        </div>
      </div>

      {/* Tab bar */}
      <div className="flex items-center gap-1 border-b px-4 py-1.5">
        {(['pieces', 'peers', 'files'] as const).map(tab => (
          <button
            key={tab}
            onClick={() => setActiveTab(tab)}
            className={`px-3 py-1 text-xs rounded-md capitalize transition-colors ${
              activeTab === tab ? 'bg-accent text-accent-foreground' : 'text-muted-foreground hover:text-foreground'
            }`}
          >
            {tab}{tab === 'peers' ? ` (${details.peers.length} / ${details.discoveredPeerCount} discovered)` : ''}
          </button>
        ))}
      </div>

      {/* Tab content */}
      <div className="flex-1 overflow-auto p-4">
        {activeTab === 'pieces' && (
          <PieceGrid pieces={details.pieces} />
        )}
        {activeTab === 'peers' && (
          <PeersList peers={details.peers} />
        )}
        {activeTab === 'files' && (
          details.files.length === 0
            ? <div className="text-sm text-muted-foreground">No files</div>
            : <FilesList
                infoHash={details.infoHash}
                files={details.files}
                onFilesUpdated={fetchDetails}
              />
        )}
      </div>
    </div>
  )
}
