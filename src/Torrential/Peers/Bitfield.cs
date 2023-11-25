namespace Torrential.Peers
{
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

        public int? SuggestPieceToDownload(Bitfield otherBitfield)
        {
            if (otherBitfield == null)
                throw new ArgumentNullException(nameof(otherBitfield));

            if (_numOfPieces != otherBitfield._numOfPieces)
                throw new ArgumentException("Bitfields must be of the same size.");

            //Randomize the offset
            var byteSlice = Random.Shared.Next(0, _sizeInBytes);


            for (int i = byteSlice; i < _sizeInBytes; i++)
            {
                var myByte = _bitfield[i];
                var otherByte = otherBitfield._bitfield[i];

                // Find a piece that exists in otherByte but not in myByte
                var diff = (byte)(otherByte & ~myByte);
                if (diff != 0)
                {
                    // Find the index of the first set bit in diff
                    var bitSlice = Random.Shared.Next(0, 8);
                    for (var bit = bitSlice; bit < 8; bit++)
                    {
                        if ((diff & (1 << bit)) != 0)
                        {
                            var pieceIndex = (i * 8) + bit;
                            if (pieceIndex < _numOfPieces) // Ensure it's a valid piece index
                            {
                                return pieceIndex;
                            }
                        }
                    }
                    for (var bit = 0; bit < bitSlice; bit++)
                    {
                        if ((diff & (1 << bit)) != 0)
                        {
                            var pieceIndex = (i * 8) + bit;
                            if (pieceIndex < _numOfPieces) // Ensure it's a valid piece index
                            {
                                return pieceIndex;
                            }
                        }
                    }
                }
            }

            for (int i = 0; i < byteSlice; i++)
            {
                var myByte = _bitfield[i];
                var otherByte = otherBitfield._bitfield[i];

                // Find a piece that exists in otherByte but not in myByte
                var diff = (byte)(otherByte & ~myByte);
                if (diff != 0)
                {
                    // Find the index of the first set bit in diff
                    var bitSlice = Random.Shared.Next(0, 8);
                    for (var bit = 0; bit < bitSlice; bit++)
                    {
                        if ((diff & (1 << bit)) != 0)
                        {
                            var pieceIndex = (i * 8) + bit;
                            if (pieceIndex < _numOfPieces) // Ensure it's a valid piece index
                            {
                                return pieceIndex;
                            }
                        }
                    }
                    for (var bit = bitSlice; bit < 8; bit++)
                    {
                        if ((diff & (1 << bit)) != 0)
                        {
                            var pieceIndex = (i * 8) + bit;
                            if (pieceIndex < _numOfPieces) // Ensure it's a valid piece index
                            {
                                return pieceIndex;
                            }
                        }
                    }
                }
            }

            return null;
        }
    }
}
