using System.Buffers;
using System.Numerics;

namespace Torrential.Peers
{
    public sealed class Bitfield : IBitfield
    {
        private readonly int _numOfPieces;
        private readonly int _sizeInBytes;
        private readonly byte[] _bitfield;

        public int NumberOfPieces => _numOfPieces;

        public byte[] Bytes => _bitfield;

        public float CompletionRatio
        {
            get
            {
                var totalPieces = (float)_numOfPieces;
                var piecesHave = _bitfield.Sum(b => BitOperations.PopCount(b));
                return piecesHave / totalPieces;
            }
        }

        public Bitfield(int numOfPieces)
        {
            _numOfPieces = numOfPieces;
            _sizeInBytes = (numOfPieces + 7) / 8;  // Calculates the total bytes needed
            _bitfield = new byte[_sizeInBytes];
        }

        public Bitfield(Span<byte> data)
        {
            _sizeInBytes = data.Length;
            _numOfPieces = _sizeInBytes * 8;
            _bitfield = new byte[_sizeInBytes];
            data.CopyTo(_bitfield);
        }

        public Bitfield(ReadOnlySequence<byte> data)
        {
            _sizeInBytes = (int)data.Length;
            _numOfPieces = _sizeInBytes * 8;
            _bitfield = new byte[_sizeInBytes];
            data.CopyTo(_bitfield);
        }

        public void Fill(Span<byte> data)
        {
            data.CopyTo(_bitfield);
        }

        public bool HasAll()
        {
            var piecesHave = _bitfield.Sum(b => BitOperations.PopCount(b));
            return piecesHave == _numOfPieces;
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
            var bitIndex = 7 - (index % 8);
            return (_bitfield[byteIndex] & (1 << bitIndex)) != 0;
        }


        /// <summary>
        /// WARNING: This method is not thread-safe, and should only be called in a single-threaded context.
        /// </summary>
        /// <param name="index"></param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public void MarkHave(int index)
        {
            if (index < 0 || index >= _numOfPieces)
                throw new ArgumentOutOfRangeException(nameof(index));

            var byteIndex = index / 8;
            var bitIndex = 7 - (index % 8);
            _bitfield[byteIndex] |= (byte)(1 << bitIndex);
        }

        public PieceSuggestionResult SuggestPieceToDownload(IBitfield peerBitfield)
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
