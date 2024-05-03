using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Torrential.Peers;

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
    }
}
