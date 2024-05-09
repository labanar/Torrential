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
        public async Task Has_all_with_partial_final_byte_async()
        {
            var myBitfield = new AsyncBitfield(2516);
            var peerBitfieldData = new byte[315];
            Array.Fill(peerBitfieldData, (byte)255);

            var peerBitfield = new AsyncBitfield(peerBitfieldData);

            //Stage my bitfield to have all but the final byte set
            var myPeerBitfieldData = new byte[314];
            Array.Fill(myPeerBitfieldData, (byte)255);
            myBitfield.Fill(myPeerBitfieldData);

            Assert.False(myBitfield.HasAll());
            var suggestedPiece = myBitfield.SuggestPieceToDownload(peerBitfield);
            Assert.NotNull(suggestedPiece.Index);

            await myBitfield.MarkHaveAsync(2512, CancellationToken.None);
            Assert.False(myBitfield.HasAll());

            await myBitfield.MarkHaveAsync(2513, CancellationToken.None);
            Assert.False(myBitfield.HasAll());

            await myBitfield.MarkHaveAsync(2514, CancellationToken.None);
            Assert.False(myBitfield.HasAll());

            await myBitfield.MarkHaveAsync(2515, CancellationToken.None);
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
        public async Task Chunk_field_with_partial_final_piece()
        {
            var meta = TorrentMetadataParser.FromFile("./debian-12.5.0-arm64-netinst.iso.torrent");
            var chunkField = new AsyncBitfield(meta.TotalNumberOfChunks);
            var segmentLength = (int)Math.Pow(2, 14);

            var chunksPerFullPiece = (int)(meta.PieceSize / Math.Pow(2, 14));
            var chunkOffset = 2105 * chunksPerFullPiece;


            var finalPieceLength = 45056;

            var chunksInThisPiece = (int)Math.Ceiling((decimal)finalPieceLength / segmentLength);


            var offset = 0;
            var extra = offset / segmentLength;
            var chunkIndex = 2105 * chunksPerFullPiece + extra;
            await chunkField.MarkHaveAsync(chunkIndex, CancellationToken.None);

            offset = (int)Math.Pow(2, 14);
            extra = offset / segmentLength;
            chunkIndex = 2105 * chunksPerFullPiece + extra;
            await chunkField.MarkHaveAsync(chunkIndex, CancellationToken.None);

            offset = (int)Math.Pow(2, 14) * 2;
            extra = offset / segmentLength;
            chunkIndex = 2105 * chunksPerFullPiece + extra;
            await chunkField.MarkHaveAsync(chunkIndex, CancellationToken.None);


            var hasAll = HasAllSegmentsForPiece(chunkField, 2105, chunksInThisPiece, chunksPerFullPiece);
            Assert.True(hasAll);
        }

        public bool HasAllSegmentsForPiece(AsyncBitfield chunkField, int pieceIndex, int chunksInThisPiece, int chunksInFullPiece)
        {
            var segmentIndex = pieceIndex * chunksInFullPiece;
            for (int i = 0; i < chunksInThisPiece; i++)
            {
                if (!chunkField.HasPiece(segmentIndex + i)) return false;
            }
            return true;
        }
    }
}
