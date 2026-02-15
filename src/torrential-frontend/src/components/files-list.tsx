import { useState } from 'react'
import { Checkbox } from '@/components/ui/checkbox'
import { updateFileSelections } from '@/lib/api'
import { formatBytes } from '@/lib/format'
import type { TorrentFileDetail } from '@/lib/api/types'

interface FilesListProps {
  infoHash: string
  files: TorrentFileDetail[]
  onFilesUpdated: () => void
}

export function FilesList({ infoHash, files, onFilesUpdated }: FilesListProps) {
  const [localFiles, setLocalFiles] = useState(files)
  const [updating, setUpdating] = useState(false)

  // Sync local state when props change (from polling)
  if (files !== localFiles && !updating) {
    setLocalFiles(files)
  }

  const selectedCount = localFiles.filter((f) => f.selected).length
  const totalCount = localFiles.length
  const allSelected = selectedCount === totalCount
  const selectedSize = localFiles
    .filter((f) => f.selected)
    .reduce((sum, f) => sum + f.fileSize, 0)
  const totalSize = localFiles.reduce((sum, f) => sum + f.fileSize, 0)

  const applySelections = async (newFiles: TorrentFileDetail[]) => {
    setLocalFiles(newFiles)
    setUpdating(true)
    try {
      await updateFileSelections(
        infoHash,
        newFiles.map((f) => ({ fileIndex: f.fileIndex, selected: f.selected }))
      )
      onFilesUpdated()
    } catch {
      // Revert on failure
      setLocalFiles(files)
    } finally {
      setUpdating(false)
    }
  }

  const toggleFile = (fileIndex: number) => {
    const newFiles = localFiles.map((f) =>
      f.fileIndex === fileIndex ? { ...f, selected: !f.selected } : f
    )
    applySelections(newFiles)
  }

  const toggleAll = () => {
    const newSelected = !allSelected
    const newFiles = localFiles.map((f) => ({ ...f, selected: newSelected }))
    applySelections(newFiles)
  }

  return (
    <div className="flex flex-col gap-2 h-full">
      {/* Header */}
      <div className="flex items-center gap-2 border-b pb-2">
        <Checkbox
          checked={allSelected}
          onCheckedChange={toggleAll}
        />
        <span className="text-sm font-medium flex-1">Name</span>
        <span className="text-sm font-medium text-muted-foreground w-20 text-right">Size</span>
      </div>

      {/* File list */}
      <div className="flex-1 overflow-auto flex flex-col gap-0.5">
        {localFiles.map((f) => (
          <div key={f.fileIndex} className="flex items-center gap-2 py-1 px-0.5 rounded hover:bg-muted/50">
            <Checkbox
              checked={f.selected}
              onCheckedChange={() => toggleFile(f.fileIndex)}
            />
            <span className="text-sm truncate flex-1" title={f.fileName}>
              {f.fileName}
            </span>
            <span className="text-xs text-muted-foreground w-20 text-right shrink-0">
              {formatBytes(f.fileSize)}
            </span>
          </div>
        ))}
      </div>

      {/* Summary */}
      <div className="border-t pt-2 text-xs text-muted-foreground">
        {selectedCount} of {totalCount} files selected ({formatBytes(selectedSize)} of{' '}
        {formatBytes(totalSize)})
      </div>
    </div>
  )
}
