using System.Buffers;
using System.Buffers.Binary;
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
        public void WriteBitfieldPacket()
        {
            var bitfieldMessage = new BitfieldMessage(new byte[] { 0, 1, 2, 3 });
            byte[] expected = new byte[9] {
                0, 0, 0, 5, //Length of 5 (1 for msgId + 4 for data)
                5, //MessageId
                0, 1, 2, 3 //Bitfield
            };
            Span<byte> buffer = new byte[9];
            BitfieldMessage.WritePacket(buffer, bitfieldMessage);
            Assert.Equal(expected, buffer.ToArray());
        }

        [Fact]
        public void WriteBitfieldPacket_FromIBitfield()
        {
            using var bitfield = new Bitfield(8);
            bitfield.MarkHave(0);
            bitfield.MarkHave(7);
            // byte should be 0b10000001 = 0x81
            var message = new BitfieldMessage(bitfield);
            Assert.Equal(2, message.MessageSize); // 1 msgId + 1 byte data

            Span<byte> buffer = new byte[6]; // MessageSize + 4 for wire format
            BitfieldMessage.WritePacket(buffer, message);
            Assert.Equal(0, buffer[0]); // length high bytes
            Assert.Equal(0, buffer[1]);
            Assert.Equal(0, buffer[2]);
            Assert.Equal(2, buffer[3]); // length = 2 (1 msgId + 1 data)
            Assert.Equal(5, buffer[4]); // msgId = Bitfield
            Assert.Equal(0x81, buffer[5]); // bitfield data
        }

        [Fact]
        public void BitfieldMessage_MessageSize_IsLengthPrefixValue()
        {
            var msg = new BitfieldMessage(new byte[10]);
            Assert.Equal(11, msg.MessageSize); // 1 msgId + 10 data
        }

        [Fact]
        public void WritePieceRequestPacket()
        {
            var request = new PieceRequestMessage(1, 16384, 16384);
            byte[] expected = new byte[17];
            BinaryPrimitives.WriteInt32BigEndian(expected.AsSpan(0), 13);    // length
            expected[4] = 6;                                                  // Request msgId
            BinaryPrimitives.WriteInt32BigEndian(expected.AsSpan(5), 1);     // index
            BinaryPrimitives.WriteInt32BigEndian(expected.AsSpan(9), 16384); // begin
            BinaryPrimitives.WriteInt32BigEndian(expected.AsSpan(13), 16384);// length

            Span<byte> buffer = new byte[17];
            PieceRequestMessage.WritePacket(buffer, request);
            Assert.Equal(expected, buffer.ToArray());
        }

        [Fact]
        public void PieceRequestMessage_FromReadOnlySequence_Roundtrip()
        {
            var original = new PieceRequestMessage(42, 8192, 16384);
            var buffer = new byte[12];
            BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(0), 42);
            BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(4), 8192);
            BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(8), 16384);

            var sequence = new ReadOnlySequence<byte>(buffer);
            var parsed = PieceRequestMessage.FromReadOnlySequence(sequence);

            Assert.Equal(original.Index, parsed.Index);
            Assert.Equal(original.Begin, parsed.Begin);
            Assert.Equal(original.Length, parsed.Length);
        }

        [Fact]
        public void WriteCancelPacket()
        {
            var cancel = new CancelMessage(2, 0, 16384);
            byte[] expected = new byte[17];
            BinaryPrimitives.WriteInt32BigEndian(expected.AsSpan(0), 13);    // length
            expected[4] = 8;                                                  // Cancel msgId
            BinaryPrimitives.WriteInt32BigEndian(expected.AsSpan(5), 2);     // index
            BinaryPrimitives.WriteInt32BigEndian(expected.AsSpan(9), 0);     // begin
            BinaryPrimitives.WriteInt32BigEndian(expected.AsSpan(13), 16384);// length

            Span<byte> buffer = new byte[17];
            CancelMessage.WritePacket(buffer, cancel);
            Assert.Equal(expected, buffer.ToArray());
        }

        [Fact]
        public void PreparedPacket_FromPeerPacket_CreatesCorrectBytes()
        {
            var have = new HaveMessage(10);
            using var prepared = PreparedPacket.FromPeerPacket(have);
            Assert.Equal(9, prepared.Size); // MessageSize(5) + 4 length prefix

            var span = prepared.PacketData;
            Assert.Equal(0, span[0]); // length big-endian
            Assert.Equal(0, span[1]);
            Assert.Equal(0, span[2]);
            Assert.Equal(5, span[3]); // length = 5
            Assert.Equal(4, span[4]); // Have msgId
            Assert.Equal(0, span[5]); // index = 10 big-endian
            Assert.Equal(0, span[6]);
            Assert.Equal(0, span[7]);
            Assert.Equal(10, span[8]);
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
