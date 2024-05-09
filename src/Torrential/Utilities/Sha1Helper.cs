using System.Buffers;
using System.IO.Pipelines;
using System.Security.Cryptography;

namespace Torrential.Utilities;
internal static class Sha1Helper
{
    public static async Task<bool> VerifyHash(PipeReader reader, byte[] expectedHash, int chunkSize)
    {
        using var hasher = SHA1.Create();
        while (true)
        {
            var result = await reader.ReadAsync();
            var bufferSequence = result.Buffer;

            while (TryReadNextChunk(ref bufferSequence, chunkSize, out var chunk))
            {

                var buffer = ArrayPool<byte>.Shared.Rent(chunkSize);
                chunk.CopyTo(buffer);
                hasher.TransformBlock(buffer, 0, chunkSize, null, 0);
                ArrayPool<byte>.Shared.Return(buffer);
            }

            reader.AdvanceTo(bufferSequence.Start, bufferSequence.End);
            if (result.IsCompleted)
            {
                hasher.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return expectedHash.AsSpan().Slice(0, 20).SequenceEqual(hasher.Hash ?? Array.Empty<byte>());
            }
        }
    }

    public static bool TryVerifyHash(PipeReader reader, byte[] expectedHash, int chunkSize)
    {
        using (var hasher = SHA1.Create())
        {
            while (true)
            {
                if (!reader.TryRead(out var result))
                    break;

                var bufferSequence = result.Buffer;
                while (TryReadNextChunk(ref bufferSequence, chunkSize, out var chunk))
                {
                    var buffer = ArrayPool<byte>.Shared.Rent(chunkSize);
                    chunk.CopyTo(buffer);
                    hasher.TransformBlock(buffer, 0, chunkSize, null, 0);
                    ArrayPool<byte>.Shared.Return(buffer);
                }

                reader.AdvanceTo(bufferSequence.Start, bufferSequence.End);
                if (result.IsCompleted)
                {
                    hasher.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    return expectedHash.AsSpan().Slice(0, 20).SequenceEqual(hasher.Hash ?? Array.Empty<byte>());
                }
            }

            if (hasher.Hash == null) return false;
            return expectedHash.AsSpan().Slice(0, 20).SequenceEqual(hasher.Hash ?? Array.Empty<byte>());
        }
    }

    private static bool TryReadNextChunk(ref ReadOnlySequence<byte> bufferSequence, int chunkSize, out ReadOnlySequence<byte> chunk)
    {
        if (bufferSequence.Length < chunkSize)
        {
            chunk = default;
            return false;
        }

        var position = bufferSequence.GetPosition(chunkSize);
        chunk = bufferSequence.Slice(0, position);
        bufferSequence = bufferSequence.Slice(chunk.End);
        return true;
    }
}
