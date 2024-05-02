import { forwardRef, useImperativeHandle, useRef } from "react";

export interface FileUploadElement {
  openFilePicker(): void;
}

export interface FileUploadProps {
  onFileChange(file: File): void;
  accept?: string | undefined;
}

export const FileUpload = forwardRef<FileUploadElement, FileUploadProps>(
  ({ onFileChange, accept }, ref) => {
    useImperativeHandle(ref, () => ({
      openFilePicker() {
        if (
          inputRef === null ||
          inputRef === undefined ||
          inputRef.current === null
        )
          return;

        inputRef.current.click();
      },
    }));

    const inputRef = useRef<HTMLInputElement | null>(null);

    return (
      <>
        <input
          type={"file"}
          accept={accept}
          hidden={true}
          style={{ display: "none" }}
          ref={inputRef}
          onChange={(e) => {
            const f = e.target.files?.item(0);
            if (f === null || f === undefined) return;
            onFileChange(f);
          }}
        />
      </>
    );
  }
);
