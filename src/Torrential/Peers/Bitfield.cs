using System.Numerics;

namespace Torrential.Peers
{
    public sealed class Bitfield
    {
        private readonly int _numOfPieces;
        private readonly SemaphoreSlim[] _semaphores;
        private readonly int _sizeInBytes;
        private readonly byte[] _bitfield;

        public int NumberOfPieces => _numOfPieces;

        public byte[] Bytes => _bitfield;

        public float CompletionRatio
        {
            get
            {
                var totalPieces = (float)_numOfPieces;
                var piecesHave = 0;

                for (var i = 0; i < _sizeInBytes; i++)
                {
                    piecesHave += BitOperations.PopCount(_bitfield[i]);
                }

                return piecesHave / totalPieces;
            }
        }

        public Bitfield(int numOfPieces)
        {
            _numOfPieces = numOfPieces;
            _sizeInBytes = (numOfPieces + 7) / 8;  // Calculates the total bytes needed
            _semaphores = new SemaphoreSlim[_sizeInBytes];
            _bitfield = new byte[_sizeInBytes];

            for (var i = 0; i < _sizeInBytes; i++)
            {
                _semaphores[i] = new SemaphoreSlim(1, 1);
            }
        }

        public Bitfield(Span<byte> data)
        {
            _sizeInBytes = data.Length;
            _numOfPieces = _sizeInBytes * 8;
            _semaphores = new SemaphoreSlim[_sizeInBytes];
            _bitfield = new byte[_sizeInBytes];
            data.CopyTo(_bitfield);

            for (var i = 0; i < _sizeInBytes; i++)
            {
                _semaphores[i] = new SemaphoreSlim(1, 1);
            }
        }

        public void Fill(Span<byte> data)
        {
            data.CopyTo(_bitfield);
        }

        public bool HasAll()
        {
            for (var i = 0; i < _sizeInBytes; i++)
            {
                var allBitsSet = (i == _sizeInBytes - 1 && _numOfPieces % 8 != 0)
                                  ? (byte)((1 << (_numOfPieces % 8)) - 1)
                                  : (byte)0xFF;

                if ((_bitfield[i] & allBitsSet) != allBitsSet)
                {
                    return false;
                }
            }

            return true;
        }

        public bool HasNone()
        {
            for (int i = 0; i < _sizeInBytes; i++)
            {
                if (_bitfield[i] != 0)
                {
                    return false;
                }
            }

            return true;
        }

        public bool HasPiece(int index)
        {
            if (index < 0 || index >= _numOfPieces)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var byteIndex = index / 8;
            var bitIndex = index % 8;
            return (_bitfield[byteIndex] & (1 << bitIndex)) != 0;
        }

        public void MarkHave(int index)
        {
            if (index < 0 || index >= _numOfPieces)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var byteIndex = index / 8;
            var bitIndex = index % 8;
            _bitfield[byteIndex] |= (byte)(1 << bitIndex);
        }


        public async Task UnmarkHaveAsync(int index, CancellationToken cancellationToken)
        {
            if (index < 0 || index >= _numOfPieces)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var byteIndex = index / 8;
            var bitIndex = index % 8;

            await _semaphores[byteIndex].WaitAsync(cancellationToken);
            try
            {
                _bitfield[byteIndex] &= (byte)~(1 << bitIndex);
            }
            finally
            {
                _semaphores[byteIndex].Release();
            }
        }

        public PieceSuggestionResult SuggestPieceToDownload(Bitfield peerBitfield)
        {
            if (HasAll())
                return PieceSuggestionResult.NoMorePieces;

            var random = Random.Shared.Next(0, _numOfPieces);

            var hasAll = HasAll();
            while (HasPiece(random) && !hasAll)
            {
                random = Random.Shared.Next(0, _numOfPieces);
                hasAll = HasAll();
            }

            return new PieceSuggestionResult(random, !hasAll);
        }
    }

    public readonly record struct PieceSuggestionResult(int? Index, bool MorePiecesAvailable)
    {
        public static PieceSuggestionResult NoMorePieces => new PieceSuggestionResult(null, false);
    }
}
