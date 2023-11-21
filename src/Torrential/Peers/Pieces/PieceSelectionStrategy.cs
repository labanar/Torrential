﻿namespace Torrential.Peers.Pieces
{
    internal class PieceSelectionStrategy(PeerId peerId, InfoHash infoHash) : IPieceSelectionStrategy
    {
        public void SuggestPiece(InfoHash hash)
        {

        }
    }


    public sealed class Bitfield
    {
        private int _numOfPieces;
        private int _sizeInBytes;
        private byte[] _bitfield;

        public Bitfield(int numOfPieces)
        {
            var fullBytes = numOfPieces / 8;
            var remaining = numOfPieces % 8;
            _numOfPieces = numOfPieces;
            _sizeInBytes = remaining == 0 ? fullBytes : fullBytes + 1;
            _bitfield = new byte[_sizeInBytes];
        }

        public Bitfield(Span<byte> data)
        {
            _sizeInBytes = data.Length;
            _numOfPieces = _sizeInBytes * 8;
            _bitfield = new byte[_sizeInBytes];
            data.CopyTo(_bitfield);
        }


        public void Fill(Span<byte> data)
        {
            data.CopyTo(_bitfield);
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
    }
}
