import { useState } from 'react'
import { X } from 'lucide-react'
import {
  ResizablePanelGroup,
  ResizablePanel,
  ResizableHandle,
} from '@/components/ui/resizable'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { PieceGrid } from '@/components/piece-grid'
import { formatBytes } from '@/lib/format'
import type { PeerDetail } from '@/lib/api/types'

interface PeersListProps {
  peers: PeerDetail[]
}

export function PeersList({ peers }: PeersListProps) {
  const [selectedPeerId, setSelectedPeerId] = useState<string | null>(null)
  const selectedPeer = peers.find((p) => p.peerId === selectedPeerId) ?? null

  return (
    <ResizablePanelGroup orientation="horizontal" className="h-full">
      <ResizablePanel defaultSize={selectedPeer ? 50 : 100} minSize={30}>
        <div className="flex flex-col gap-1 overflow-auto h-full pr-2">
          {peers.length === 0 && (
            <div className="text-sm text-muted-foreground">No connected peers</div>
          )}
          {peers.map((peer) => {
            const isSelected = peer.peerId === selectedPeerId
            return (
              <div
                key={peer.peerId}
                onClick={() => setSelectedPeerId(isSelected ? null : peer.peerId)}
                className={`flex items-center gap-3 rounded-md border px-3 py-2 cursor-pointer transition-colors text-sm ${
                  isSelected
                    ? 'bg-accent border-accent-foreground/20'
                    : 'hover:bg-muted/50'
                }`}
              >
                <div className="flex flex-col gap-0.5 flex-1 min-w-0">
                  <div className="flex items-center gap-2">
                    <span className="font-mono truncate">
                      {peer.ipAddress}:{peer.port}
                    </span>
                    {peer.isSeed && (
                      <Badge className="bg-green-600 text-white text-[10px] px-1.5 py-0">
                        Seed
                      </Badge>
                    )}
                  </div>
                  <div className="flex items-center gap-3 text-xs text-muted-foreground">
                    <span>{(peer.progress * 100).toFixed(1)}%</span>
                    <span>↓ {formatBytes(peer.bytesDownloaded)}</span>
                    <span>↑ {formatBytes(peer.bytesUploaded)}</span>
                  </div>
                </div>
              </div>
            )
          })}
        </div>
      </ResizablePanel>

      {selectedPeer && (
        <>
          <ResizableHandle withHandle />
          <ResizablePanel defaultSize={50} minSize={25}>
            <div className="flex flex-col gap-4 overflow-auto h-full pl-4">
              <div className="flex items-center justify-between">
                <h3 className="text-sm font-medium">Peer Details</h3>
                <Button
                  variant="ghost"
                  size="icon"
                  className="size-6"
                  onClick={() => setSelectedPeerId(null)}
                >
                  <X className="size-3.5" />
                </Button>
              </div>

              <div className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1.5 text-sm">
                <span className="text-muted-foreground">Peer ID</span>
                <span className="font-mono text-xs truncate">{selectedPeer.peerId}</span>

                <span className="text-muted-foreground">IP Address</span>
                <span className="font-mono">{selectedPeer.ipAddress}:{selectedPeer.port}</span>

                <span className="text-muted-foreground">Role</span>
                <span>{selectedPeer.isSeed ? 'Seed' : 'Leecher'}</span>

                <span className="text-muted-foreground">Downloaded</span>
                <span>{formatBytes(selectedPeer.bytesDownloaded)}</span>

                <span className="text-muted-foreground">Uploaded</span>
                <span>{formatBytes(selectedPeer.bytesUploaded)}</span>

                <span className="text-muted-foreground">Progress</span>
                <span>{(selectedPeer.progress * 100).toFixed(1)}%</span>
              </div>

              {selectedPeer.pieces.length > 0 && (
                <div>
                  <h4 className="text-xs font-medium text-muted-foreground mb-2">
                    Piece Availability
                  </h4>
                  <PieceGrid pieces={selectedPeer.pieces} />
                </div>
              )}
            </div>
          </ResizablePanel>
        </>
      )}
    </ResizablePanelGroup>
  )
}
