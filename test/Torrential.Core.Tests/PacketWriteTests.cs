using Torrential.Core.Peers;

namespace Torrential.Core.Tests
{
    public class PacketWriteTests
    {

        [Fact]
        public void WriteChoke()
        {
            var chokeMessage = new ChokeMessage();
            AssertActionPacketWrite(chokeMessage, 0);
        }

        [Fact]
        public void WriteUnchoke()
        {
            var unchokeMessage = new UnchokeMessage();
            AssertActionPacketWrite(unchokeMessage, 1);
        }

        [Fact]
        public void WriteInterested()
        {
            var interestedMessage = new InterestedMessage();
            AssertActionPacketWrite(interestedMessage, 2);
        }

        [Fact]
        public void WriteNotInterested()
        {
            var notInterestedMessage = new NotInterestedMessage();
            AssertActionPacketWrite(notInterestedMessage, 3);
        }


        [Fact]
        public void WriteHavePacket()
        {
            var haveMessage = new HaveMessage(5);
            byte[] expected = new byte[9] {
                0, 0, 0, 5, //Length of 5
                4, //MessageId
                0, 0, 0, 5  //Index
            };

            Span<byte> buffer = new byte[9];
            HaveMessage.WritePacket(buffer, haveMessage);
            Assert.Equal(expected, buffer.ToArray());
        }

        [Fact]
        public void WriteBitFieldPacket()
        {
            //var bitFieldMessage = new Bitfie(new byte[] { 0, 1, 2, 3 });
            //byte[] expected = new byte[9] {
            //    0, 0, 0, 5, //Length of 5
            //    5, //MessageId
            //    0, 1, 2, 3 //Bitfield
            //};
            //Span<byte> buffer = new byte[9];
            //BitFieldMessage.WritePacket(buffer, bitFieldMessage);
            //Assert.Equal(expected, buffer.ToArray());
        }



        //All action packets should have a 5 byte message:
        // 4 bytes for length, 1 byte for message id
        private static void AssertActionPacketWrite<T>(T packet, byte expectedHeader)
            where T : IPeerActionPacket<T>, allows ref struct
        {
            byte[] expected = new byte[5] {
                0, 0, 0, 1, //Length of 1
                expectedHeader //MessageId
            };
            Span<byte> buffer = new byte[5];
            T.WritePacket(buffer, packet);
            Assert.Equal([.. expected], [.. buffer]);
        }
    }
}
