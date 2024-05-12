using System.Net;
using System.Net.Sockets;
using Torrential.Trackers;

namespace Torrential.Utilities;

internal static class SocketExtensions
{
    public static PeerInfo GetPeerInfo(this Socket socket)
    {
        var ipEndPoint = (IPEndPoint)socket.RemoteEndPoint;
        return new PeerInfo
        {
            Ip = ipEndPoint.Address,
            Port = ipEndPoint.Port
        };
    }
}
