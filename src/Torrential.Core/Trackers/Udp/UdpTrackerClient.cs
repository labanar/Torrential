using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace Torrential.Core.Trackers.Udp;

public class UdpTrackerClient : ITrackerClient
{
    public bool IsValidAnnounceForClient(string announceUrl)
    {
        return announceUrl.StartsWith("udp");
    }

    public Task<AnnounceResponse> Announce(AnnounceRequest request)
    {
        var uri = new Uri(request.Url);
        using var client = new UdpClient(uri.Host, uri.Port);
        var connectionResponse = SendConnectRequest(client);
        var announceResponse = SendAnnounceRequest(client, connectionResponse.ConnectionId, connectionResponse.TransactionId, request.InfoHash, request.PeerId, 0, 0, 0, 2, 0, 0, request.NumWant, 0);
        return Task.FromResult(announceResponse);
    }

    private UdpConnectionResponse SendConnectRequest(UdpClient client)
    {
        Span<byte> buffer = stackalloc byte[16];
        var transactionId = Random.Shared.Next(0, 65535);
        buffer.TryWriteBigEndian(0x41727101980);
        buffer[12..].TryWriteBigEndian(transactionId);

        client.Send(buffer);
        IPEndPoint endPoint = null;
        var recBuff = client.Receive(ref endPoint);

        if (recBuff == null)
            return UdpConnectionResponse.Failure(UdpConnectionError.NoResponse);
        if (recBuff.Length < 0)
            return UdpConnectionResponse.Failure(UdpConnectionError.NoData);
        if (recBuff.Length < 16)
            return UdpConnectionResponse.Failure(UdpConnectionError.PartialData);

        var connectionId = recBuff.AsSpan().Slice(8).ReadBigEndianInt64();
        return UdpConnectionResponse.Success(transactionId, connectionId);
    }

    private AnnounceResponse SendAnnounceRequest(UdpClient client, long connectionId, int transactionId, InfoHash infoHash, PeerId peerId, long downloaded, long left, long uploaded, int @event, int ipAddress, int key, int num_want, short port)
    {
        Span<byte> buffer = stackalloc byte[98];
        buffer[..8].TryWriteBigEndian(connectionId);
        buffer[8..].TryWriteBigEndian(1);
        infoHash.CopyTo(buffer[16..]);
        peerId.CopyTo(buffer[36..]);
        buffer[56..].TryWriteBigEndian(downloaded);
        buffer[64..].TryWriteBigEndian(left);
        buffer[72..].TryWriteBigEndian(uploaded);
        buffer[80..].TryWriteBigEndian(@event);
        buffer[84..].TryWriteBigEndian(0);
        buffer[88..].TryWriteBigEndian(key);
        buffer[92..].TryWriteBigEndian(num_want);
        BinaryPrimitives.WriteInt16BigEndian(buffer[96..], port);

        client.Send(buffer);
        IPEndPoint endPoint = null;
        var recBuff = client.Receive(ref endPoint);

        if (recBuff == null)
            throw new Exception("UdpClient failed to receive");

        if (recBuff.Length < 0)
            throw new Exception("UdpClient received no response");

        if (recBuff.Length < 20)
            throw new Exception("UdpClient received a partial response");

        if ((recBuff.Length - 20) % 6 != 0)
            throw new Exception("UdpClient received a partial response");

        var peers = new List<PeerInfo>();
        var numOfPeers = (recBuff.Length - 20) / 6;
        for (int n = 0; n < numOfPeers; n++)
        {
            var ipBytes = recBuff.AsSpan().Slice(20 + (6 * n), 4);
            var portBytes = recBuff.AsSpan().Slice(24 + (6 * n), 2);
            var ip = new IPAddress(ipBytes);
            var peerPort = portBytes.ReadBigEndianUInt16();
            peers.Add(new PeerInfo(ip, peerPort));
        }

        return new AnnounceResponse
        {
            Interval = recBuff.AsSpan()[8..].ReadBigEndianInt32(),
            Peers = peers
        };
    }

    internal enum UdpConnectionError
    {
        NoResponse,
        NoData,
        PartialData
    }

    internal readonly struct UdpConnectionResponse
    {
        public readonly int TransactionId;
        public readonly long ConnectionId;
        public readonly UdpConnectionError? Error;
        public readonly bool HasError;

        public static UdpConnectionResponse Success(int transactionId, long connectionId) => new UdpConnectionResponse(transactionId, connectionId);
        public static UdpConnectionResponse Failure(UdpConnectionError error) => new UdpConnectionResponse(error);

        private UdpConnectionResponse(int transactionId, long connectionId)
        {
            TransactionId = transactionId;
            ConnectionId = connectionId;
            HasError = false;
        }

        private UdpConnectionResponse(UdpConnectionError error)
        {
            Error = error;
            HasError = true;
        }
    }
}
