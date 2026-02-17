using Microsoft.Win32.SafeHandles;

namespace Torrential.Files;

internal sealed class PartFileSegmentReadStream(SafeFileHandle fileHandle, long fileStartOffset, long segmentLength, bool leaveOpen = true) : Stream
{
    private readonly SafeFileHandle _fileHandle = fileHandle ?? throw new ArgumentNullException(nameof(fileHandle));
    private readonly long _segmentStart = fileStartOffset >= 0 ? fileStartOffset : throw new ArgumentOutOfRangeException(nameof(fileStartOffset));
    private readonly long _segmentLength = segmentLength >= 0 ? segmentLength : throw new ArgumentOutOfRangeException(nameof(segmentLength));
    private readonly bool _leaveOpen = leaveOpen;
    private long _position;
    private bool _disposed;

    public override bool CanRead => !_disposed;
    public override bool CanSeek => !_disposed;
    public override bool CanWrite => false;
    public override long Length
    {
        get
        {
            ThrowIfDisposed();
            return _segmentLength;
        }
    }

    public override long Position
    {
        get
        {
            ThrowIfDisposed();
            return _position;
        }
        set
        {
            ThrowIfDisposed();
            if (value < 0 || value > _segmentLength)
                throw new ArgumentOutOfRangeException(nameof(value));

            _position = value;
        }
    }

    public override int Read(Span<byte> buffer)
    {
        ThrowIfDisposed();
        if (buffer.Length == 0)
            return 0;

        var remaining = _segmentLength - _position;
        if (remaining <= 0)
            return 0;

        var bytesToRead = (int)Math.Min(remaining, buffer.Length);
        var read = RandomAccess.Read(_fileHandle, buffer[..bytesToRead], _segmentStart + _position);
        _position += read;
        return read;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (offset + count > buffer.Length)
            throw new ArgumentException("The sum of offset and count is larger than buffer length.");

        return Read(buffer.AsSpan(offset, count));
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (buffer.Length == 0)
            return 0;

        var remaining = _segmentLength - _position;
        if (remaining <= 0)
            return 0;

        var bytesToRead = (int)Math.Min(remaining, buffer.Length);
        var read = await RandomAccess.ReadAsync(_fileHandle, buffer[..bytesToRead], _segmentStart + _position, cancellationToken);
        _position += read;
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        ThrowIfDisposed();
        var target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _segmentLength + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };

        if (target < 0 || target > _segmentLength)
            throw new IOException("Attempted to seek outside the segment bounds.");

        _position = target;
        return _position;
    }

    public override void Flush()
    {
    }

    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing && !_leaveOpen)
            _fileHandle.Close();

        _disposed = true;
        base.Dispose(disposing);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
