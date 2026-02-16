using Torrential.Peers;
using Torrential.Torrents;

namespace Torrential.Tests
{

    public class BitfieldTests
    {
        [Fact]
        public void Has_all_with_partial_final_byte()
        {
            var myBitfield = new Bitfield(2516);
            var peerBitfieldData = new byte[315];
            Array.Fill(peerBitfieldData, (byte)255);

            var peerBitfield = new Bitfield(peerBitfieldData);

            //Stage my bitfield to have all but the final byte set
            var myPeerBitfieldData = new byte[314];
            Array.Fill(myPeerBitfieldData, (byte)255);
            myBitfield.Fill(myPeerBitfieldData);

            Assert.False(myBitfield.HasAll());
            var suggestedPiece = myBitfield.SuggestPieceToDownload(peerBitfield);
            Assert.NotNull(suggestedPiece.Index);

            myBitfield.MarkHave(2512);
            Assert.False(myBitfield.HasAll());

            myBitfield.MarkHave(2513);
            Assert.False(myBitfield.HasAll());

            myBitfield.MarkHave(2514);
            Assert.False(myBitfield.HasAll());

            myBitfield.MarkHave(2515);
            Assert.True(myBitfield.HasAll());
            Assert.Equal(1.0, myBitfield.CompletionRatio, 0);

            //We should no longer be suggesting pieces if we have the all
            Assert.Null(myBitfield.SuggestPieceToDownload(peerBitfield).Index);
        }


        [Fact]
        public void Has_all_with_partial_final_byte_thread_safe()
        {
            var myBitfield = new Bitfield(2516);
            var peerBitfieldData = new byte[315];
            Array.Fill(peerBitfieldData, (byte)255);

            var peerBitfield = new Bitfield(peerBitfieldData);

            //Stage my bitfield to have all but the final byte set
            var myPeerBitfieldData = new byte[314];
            Array.Fill(myPeerBitfieldData, (byte)255);
            myBitfield.Fill(myPeerBitfieldData);

            Assert.False(myBitfield.HasAll());
            var suggestedPiece = myBitfield.SuggestPieceToDownload(peerBitfield);
            Assert.NotNull(suggestedPiece.Index);

            myBitfield.MarkHave(2512);
            Assert.False(myBitfield.HasAll());

            myBitfield.MarkHave(2513);
            Assert.False(myBitfield.HasAll());

            myBitfield.MarkHave(2514);
            Assert.False(myBitfield.HasAll());

            myBitfield.MarkHave(2515);
            Assert.True(myBitfield.HasAll());
            Assert.Equal(1.0, myBitfield.CompletionRatio, 0);

            //We should no longer be suggesting pieces if we have the all
            Assert.Null(myBitfield.SuggestPieceToDownload(peerBitfield).Index);
        }


        [Fact]
        public void Has_all_with_partial_final_byte_alt()
        {
            var myBitfield = new Bitfield(2106);
            var peerBitfieldData = new byte[264];
            Array.Fill(peerBitfieldData, (byte)255);

            var peerBitfield = new Bitfield(peerBitfieldData);

            //Stage my bitfield to have all but the final byte set
            var myPeerBitfieldData = new byte[263];
            Array.Fill(myPeerBitfieldData, (byte)255);
            myBitfield.Fill(myPeerBitfieldData);

            Assert.False(myBitfield.HasAll());
            var suggestedPiece = myBitfield.SuggestPieceToDownload(peerBitfield);
            Assert.NotNull(suggestedPiece.Index);

            myBitfield.MarkHave(2104);
            Assert.False(myBitfield.HasAll());

            myBitfield.MarkHave(2105);
            Assert.True(myBitfield.HasAll());
            Assert.Equal(1.0, myBitfield.CompletionRatio, 0);

            //We should no longer be suggesting pieces if we have the all
            Assert.Null(myBitfield.SuggestPieceToDownload(peerBitfield).Index);
        }


        [Fact]
        public void Chunk_field_with_partial_final_piece()
        {
            var meta = TorrentMetadataParser.FromFile("./debian-12.5.0-arm64-netinst.iso.torrent");
            var chunkField = new Bitfield(meta.TotalNumberOfChunks);
            var blockLength = (int)Math.Pow(2, 14);

            var chunksPerFullPiece = (int)(meta.PieceSize / Math.Pow(2, 14));
            var chunkOffset = 2105 * chunksPerFullPiece;


            var finalPieceLength = 45056;

            var chunksInThisPiece = (int)Math.Ceiling((decimal)finalPieceLength / blockLength);


            var offset = 0;
            var extra = offset / blockLength;
            var chunkIndex = 2105 * chunksPerFullPiece + extra;
            chunkField.MarkHave(chunkIndex);

            offset = (int)Math.Pow(2, 14);
            extra = offset / blockLength;
            chunkIndex = 2105 * chunksPerFullPiece + extra;
            chunkField.MarkHave(chunkIndex);

            offset = (int)Math.Pow(2, 14) * 2;
            extra = offset / blockLength;
            chunkIndex = 2105 * chunksPerFullPiece + extra;
            chunkField.MarkHave(chunkIndex);


            var hasAll = HasAllBlocksForPiece(chunkField, 2105, chunksInThisPiece, chunksPerFullPiece);
            Assert.True(hasAll);
        }

        public bool HasAllBlocksForPiece(Bitfield chunkField, int pieceIndex, int chunksInThisPiece, int chunksInFullPiece)
        {
            var blockIndex = pieceIndex * chunksInFullPiece;
            for (int i = 0; i < chunksInThisPiece; i++)
            {
                if (!chunkField.HasPiece(blockIndex + i)) return false;
            }
            return true;
        }
    }
}
