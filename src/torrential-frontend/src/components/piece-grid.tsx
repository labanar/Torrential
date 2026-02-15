import { useMemo } from 'react'
import { cn } from '@/lib/utils'

interface PieceGridProps {
  pieces: boolean[]
  className?: string
}

const GROUPING_THRESHOLD = 2000

function getGreenShade(ratio: number): string {
  if (ratio === 0) return 'bg-zinc-700 dark:bg-zinc-800'
  if (ratio <= 0.25) return 'bg-green-900'
  if (ratio <= 0.5) return 'bg-green-700'
  if (ratio <= 0.75) return 'bg-green-500'
  return 'bg-green-400'
}

export function PieceGrid({ pieces, className }: PieceGridProps) {
  const { cells, downloadedCount, totalCount } = useMemo(() => {
    const total = pieces.length
    const downloaded = pieces.filter(Boolean).length

    if (total <= GROUPING_THRESHOLD) {
      return {
        cells: pieces.map((have, i) => ({ index: i, have, label: `Piece ${i}` })),
        downloadedCount: downloaded,
        totalCount: total,
      }
    }

    // Grouped mode: each cell represents a chunk of pieces
    const chunkSize = Math.ceil(total / Math.min(total, 1000))
    const grouped: { index: number; ratio: number; label: string }[] = []
    for (let i = 0; i < total; i += chunkSize) {
      const end = Math.min(i + chunkSize, total)
      const chunkHave = pieces.slice(i, end).filter(Boolean).length
      const chunkTotal = end - i
      grouped.push({
        index: i,
        ratio: chunkHave / chunkTotal,
        label: `Pieces ${i}–${end - 1}: ${chunkHave}/${chunkTotal}`,
      })
    }

    return { cells: grouped, downloadedCount: downloaded, totalCount: total }
  }, [pieces])

  const percentage = totalCount > 0 ? ((downloadedCount / totalCount) * 100).toFixed(1) : '0.0'
  const isGrouped = totalCount > GROUPING_THRESHOLD

  return (
    <div className={cn('flex flex-col gap-2', className)}>
      <div className="text-xs text-muted-foreground">
        {downloadedCount} / {totalCount} pieces ({percentage}%)
      </div>
      <div className="flex flex-wrap gap-[2px]">
        {isGrouped
          ? (cells as { index: number; ratio: number; label: string }[]).map((cell) => (
              <div
                key={cell.index}
                title={cell.label}
                className={cn('size-2.5 rounded-[1px]', getGreenShade(cell.ratio))}
              />
            ))
          : (cells as { index: number; have: boolean; label: string }[]).map((cell) => (
              <div
                key={cell.index}
                title={cell.label}
                className={cn(
                  'size-2.5 rounded-[1px]',
                  cell.have
                    ? 'bg-green-500 dark:bg-green-600'
                    : 'bg-muted dark:bg-zinc-800'
                )}
              />
            ))}
      </div>
    </div>
  )
}
