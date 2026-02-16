import { useEffect, useMemo, useState } from "react";
import {
  Modal,
  ModalOverlay,
  ModalContent,
  ModalHeader,
  ModalCloseButton,
  ModalBody,
  ModalFooter,
  Button,
  Checkbox,
  Text,
} from "@chakra-ui/react";
import { TorrentPreview } from "../../services/api";

function prettyPrintBytes(bytes: number) {
  const sizes = ["Bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB"];
  if (bytes === 0) return "0 Bytes";
  const i = Math.floor(Math.log(bytes) / Math.log(1024));
  if (i === 0) return bytes + " " + sizes[i];
  return (bytes / Math.pow(1024, i)).toFixed(2) + " " + sizes[i];
}

interface TorrentFilePreviewModalProps {
  isOpen: boolean;
  onClose: () => void;
  onConfirm: (selectedFileIds: number[]) => void;
  preview: TorrentPreview | null;
}

export function TorrentFilePreviewModal({
  isOpen,
  onClose,
  onConfirm,
  preview,
}: TorrentFilePreviewModalProps) {
  const [selectedFileIds, setSelectedFileIds] = useState<Set<number>>(
    new Set()
  );

  useEffect(() => {
    if (preview) {
      setSelectedFileIds(new Set(preview.files.map((f) => f.id)));
    }
  }, [preview]);

  const allSelected =
    preview !== null &&
    preview.files.length > 0 &&
    selectedFileIds.size === preview.files.length;

  const someSelected =
    selectedFileIds.size > 0 && !allSelected;

  const selectedSize = useMemo(() => {
    if (!preview) return 0;
    return preview.files
      .filter((f) => selectedFileIds.has(f.id))
      .reduce((sum, f) => sum + f.fileSize, 0);
  }, [preview, selectedFileIds]);

  const toggleFile = (id: number) => {
    setSelectedFileIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  };

  const toggleAll = () => {
    if (!preview) return;
    if (allSelected) {
      setSelectedFileIds(new Set());
    } else {
      setSelectedFileIds(new Set(preview.files.map((f) => f.id)));
    }
  };

  const handleConfirm = () => {
    onConfirm(Array.from(selectedFileIds));
  };

  if (!preview) return null;

  return (
    <Modal isOpen={isOpen} onClose={onClose} size="xl">
      <ModalOverlay />
      <ModalContent fontFamily="'Courier New', Courier, monospace">
        <ModalHeader>{preview.name}</ModalHeader>
        <ModalCloseButton />
        <ModalBody display="flex" flexDirection="column" gap={3}>
          <Text fontSize="sm">
            Total size: {prettyPrintBytes(preview.totalSize)}
          </Text>
          <Checkbox
            isChecked={allSelected}
            isIndeterminate={someSelected}
            onChange={toggleAll}
            fontWeight="bold"
          >
            Select All
          </Checkbox>
          <div
            style={{
              maxHeight: "400px",
              overflowY: "auto",
              display: "flex",
              flexDirection: "column",
              gap: "4px",
            }}
          >
            {preview.files.map((file) => (
              <Checkbox
                key={file.id}
                isChecked={selectedFileIds.has(file.id)}
                onChange={() => toggleFile(file.id)}
              >
                <Text fontSize="sm">
                  {file.filename}{" "}
                  <Text as="span" color="gray.500">
                    ({prettyPrintBytes(file.fileSize)})
                  </Text>
                </Text>
              </Checkbox>
            ))}
          </div>
          <Text fontSize="sm" fontWeight="bold">
            Selected: {prettyPrintBytes(selectedSize)} ({selectedFileIds.size}{" "}
            {selectedFileIds.size === 1 ? "file" : "files"})
          </Text>
        </ModalBody>
        <ModalFooter>
          <Button
            colorScheme="blue"
            mr={3}
            onClick={handleConfirm}
            isDisabled={selectedFileIds.size === 0}
          >
            Add Torrent
          </Button>
          <Button onClick={onClose}>Cancel</Button>
        </ModalFooter>
      </ModalContent>
    </Modal>
  );
}
