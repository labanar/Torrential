using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Torrential.Peers
{
    public static class PeerMetrics
    {
        public const string MeterName = "Torrential.Peers";

        internal static readonly Meter PeerConnectionsMeter = new(MeterName);

        private static UpDownCounter<int> CONNECTED_PEERS_COUNT = PeerConnectionsMeter.CreateUpDownCounter<int>("connected_peers_count", "peers", "The number of peers currently connected");
        internal static UpDownCounter<int> HALF_OPEN_CONNECTIONS_COUNT = PeerConnectionsMeter.CreateUpDownCounter<int>("half_open_connections_count", "connections", "The number of half open connections");

        private static Counter<long> INGRESS_BYTES = PeerConnectionsMeter.CreateCounter<long>("ingress_bytes", "bytes", "The number of bytes received from peers");
        private static Counter<long> EGRESS_BYTES = PeerConnectionsMeter.CreateCounter<long>("egress_bytes", "bytes", "The number of bytes received from peers");


        internal static void IncrementConnectedPeersCount(InfoHash infoHash)
        {
            var tags = new TagList
            {
                {"info_hash", infoHash.AsString() }
            };
            CONNECTED_PEERS_COUNT.Add(1, tags);
        }

        internal static void DecrementConnectedPeersCount(InfoHash infoHash)
        {
            var tags = new TagList
            {
                {"info_hash", infoHash.AsString() }
            };
            CONNECTED_PEERS_COUNT.Add(-1, tags);
        }


        internal static void IncrementIngressBytes(InfoHash infoHash, long bytesObserved)
        {
            var tags = new TagList
            {
                {"info_hash", infoHash.AsString() }
            };
            INGRESS_BYTES.Add(bytesObserved, tags);
        }

        internal static void IncrementEgressBytes(InfoHash infoHash, long bytesObserved)
        {
            var tags = new TagList
            {
                {"info_hash", infoHash.AsString() }
            };
            EGRESS_BYTES.Add(bytesObserved, tags);
        }
    }
}
