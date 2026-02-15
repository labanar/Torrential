import { useEffect, useState } from 'react'
import { getTorrentDetails } from '@/lib/api'
import type { TorrentDetails } from '@/lib/api'

interface TorrentInfoPaneProps {
  infoHash: string
}

export function TorrentInfoPane({ infoHash }: TorrentInfoPaneProps) {
  const [details, setDetails] = useState<TorrentDetails | null>(null)
  const [activeTab, setActiveTab] = useState<'pieces' | 'peers' | 'files'>('pieces')

  useEffect(() => {
    let cancelled = false
    const fetchDetails = async () => {
      const data = await getTorrentDetails(infoHash)
      if (!cancelled) setDetails(data)
    }
    fetchDetails()
    const interval = setInterval(fetchDetails, 5000)
    return () => { cancelled = true; clearInterval(interval) }
  }, [infoHash])

  if (!details) return <div className="p-4 text-muted-foreground">Loading...</div>

  return (
    <div className="flex h-full flex-col overflow-hidden">
      {/* Tab bar */}
      <div className="flex items-center gap-1 border-b px-4 py-2">
        <span className="text-sm font-medium mr-4 truncate">{details.name}</span>
        {(['pieces', 'peers', 'files'] as const).map(tab => (
          <button
            key={tab}
            onClick={() => setActiveTab(tab)}
            className={`px-3 py-1 text-xs rounded-md capitalize ${
              activeTab === tab ? 'bg-accent text-accent-foreground' : 'text-muted-foreground hover:text-foreground'
            }`}
          >
            {tab}{tab === 'peers' ? ` (${details.peers.length})` : ''}
          </button>
        ))}
      </div>

      {/* Tab content */}
      <div className="flex-1 overflow-auto p-4">
        {activeTab === 'pieces' && <div>Piece map placeholder</div>}
        {activeTab === 'peers' && <div>Peers list placeholder</div>}
        {activeTab === 'files' && <div>Files list placeholder</div>}
      </div>
    </div>
  )
}
