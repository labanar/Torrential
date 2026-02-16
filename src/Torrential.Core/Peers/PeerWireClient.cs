using Microsoft.Extensions.Logging;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading.Channels;

namespace Torrential.Core.Peers;

public class PeerWireClient(
    IPeer peer,
    ILogger logger,
    CancellationToken torrentStoppingToken)
{
    private readonly CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(torrentStoppingToken);


    //Need to queue this message and send it in order
    private readonly Channel<PreparedPacket> OutboundChannel = Channel.CreateBounded<PreparedPacket>(new BoundedChannelOptions(10)
    {
        SingleReader = true,
        SingleWriter = false,
        AllowSynchronousContinuations = false,
        FullMode = BoundedChannelFullMode.Wait
    });

    public async ValueTask QueuePacket<T>(T packet)
        where T : IPeerPacket<T>
    {
        await OutboundChannel.Writer.WriteAsync(PreparedPacket.FromPeerPacket(packet), CancellationToken.None);
    }

    public async Task ProcessMessages()
    {
        var readerTask = ProcessReads(cts.Token);
        var writeTask = ProcessWrites(cts.Token);

        try
        {
            await Task.WhenAll(readerTask, writeTask);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing peer connection {PeerId}", "PEERID");
        }


        OutboundChannel.Writer.Complete();
        //INBOUND_BLOCK_CHANNEL.Writer.Complete();
        //PeerPeieceRequests.Writer.Complete();

        //Clear out any disposables stuck in the channels
        await foreach (var pak in OutboundChannel.Reader.ReadAllAsync())
            pak.Dispose();

        //await foreach (var pak in INBOUND_BLOCK_CHANNEL.Reader.ReadAllAsync())
        //    pak.Dispose();

        //await foreach (var _ in PeerPeieceRequests.Reader.ReadAllAsync()) ;
    }

    private async Task ProcessReads(CancellationToken cancellationToken)
    {
        //TODO - enforce global and peer rate limits here
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                ReadResult result = await peer.Reader.ReadAsync(cancellationToken);
                ReadOnlySequence<byte> buffer = result.Buffer;

                while (TryReadMessage(ref buffer, out var messageSize, out var messageId, out ReadOnlySequence<byte> payload))
                {
                    if (!await ProcessAsync(messageSize, messageId, payload))
                    {
                        logger.LogInformation("Failed to process message {MessageId} {MessageSize} - ending peer connection read processor", messageId, messageSize);
                        return;
                    }
                }

                // Tell the PipeReader how much of the buffer has been consumed.
                peer.Reader.AdvanceTo(buffer.Start, buffer.End);

                // Stop reading if there's no more data coming.
                if (result.IsCompleted)
                {
                    logger.LogWarning("No further data from peer");
                    return;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error reading from peer");
                return;
            }
        }

        logger.LogInformation("Ending peer connection read processor");
    }

    private async Task ProcessWrites(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var pak in OutboundChannel.Reader.ReadAllAsync(cancellationToken))
            {
                using (pak)
                {
                    try
                    {
                        peer.Writer.Write(pak.PacketData);
                        await peer.Writer.FlushAsync(cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError("Failed to write message to {PeerId} - ending peer connection write processor", "PEERID");
                        return;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing writes");
        }

        logger.LogInformation("Ending peer connection write processor");

    }
    private bool TryReadMessage(ref ReadOnlySequence<byte> buffer, out int messageSize, out byte messageId, out ReadOnlySequence<byte> payload)
    {
        payload = default;
        messageId = 0;
        messageSize = 0;

        //Make sure that we have at least 4 bytes
        //4 for the message size (32-bit) unsigned int
        if (buffer.Length < 4)
            return false;

        var sequenceReader = new SequenceReader<byte>(buffer);

        if (!sequenceReader.TryReadBigEndian(out messageSize))
        {
            logger.LogDebug("Failed to read message size");
            return false;
        }

        if (messageSize == 0)
        {
            logger.LogDebug("Keep alive recieved");
            buffer = buffer.Slice(4);
            return true;
        }

        if (!sequenceReader.TryRead(out messageId))
        {
            logger.LogDebug("Failed to read messageId");
            return false;
        }

        if (messageSize == 1)
        {
            buffer = buffer.Slice(5);
            return true;
        }
        else if (!sequenceReader.TryReadExact(messageSize - 1, out payload))
        {
            logger.LogDebug("Failed to read message payload");
            return false;
        }

        buffer = buffer.Slice(payload.End);
        return true;
    }


    private async ValueTask<bool> ProcessAsync(int messageSize, byte messageId, ReadOnlySequence<byte> payload)
    {
        //Discard keep alive messages
        if (messageId == 0 && messageSize == 0)
        {
            ///*LastMessageTimestamp*/ = DateTimeOffset.UtcNow;
            return true;
        }


        //Certain messages can be processed immediately, so we'll handle those upfront
        bool handled = true;



        //switch (messageId)
        //{
        //    case PeerWireMessageType.Choke:
        //        HandleChoke();
        //        handled = true;
        //        break;
        //    case PeerWireMessageType.Unchoke:
        //        HandleUnchoke();
        //        handled = true;
        //        break;
        //    case PeerWireMessageType.Interested:
        //        HandleInterested();
        //        handled = true;
        //        break;
        //    case PeerWireMessageType.NotInterested:
        //        HandleNotInterested();
        //        handled = true;
        //        break;
        //    case PeerWireMessageType.Bitfield:
        //        HandleBitfield(payload);
        //        handled = true;
        //        break;
        //    case PeerWireMessageType.Have:
        //        HandleHave(payload);
        //        handled = true;
        //        break;
        //    case PeerWireMessageType.Piece:
        //        handled = await HandlePieceAsync(payload, messageSize - 9);
        //        break;
        //    case PeerWireMessageType.Request:
        //        handled = await HandleRequestAsync(payload);
        //        break;
        //    case PeerWireMessageType.Cancel:
        //        handled = HandleCancel(payload);
        //        break;
        //    default:
        //        _logger.LogWarning("Unhandled message type {MessageType}", messageId);
        //        handled = true;
        //        break;
        //        ////Return false here when we cannot process the message. It tells the main loop to disconnect and stop comms with this peer
        //        ////For now we'll return true, but we should change this to false once we've implemented all the message handlers
        //        //handled = true;
        //        //break;
        //}

        ////If the peer bitfield is null, then we can assume that the peer has an empty bitfield
        ////_state.PeerBitfield ??= new Bitfield(_meta.NumberOfPieces);
        //LastMessageTimestamp = DateTimeOffset.UtcNow;
        return handled;
    }

}
