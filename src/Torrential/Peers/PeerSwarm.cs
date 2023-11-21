using System.Collections.Concurrent;

namespace Torrential.Peers
{
    /// <summary>
    /// Should be responsible for the following:
    /// 
    /// 1) Re-announcing on some cadence
    /// 2) Keeping the swarm healthy (removing bad peers, connecting to new ones when we're under the limit)
    /// 3) Central place for us to dispatch have messages to peers (once we downlod a piece we need to inform the peers we have it now)
    /// 4) 
    /// 
    /// </summary>
    public sealed class PeerSwarm
    {
        public ConcurrentDictionary<InfoHash, PeerWireConnection> _peerSwarms = new ConcurrentDictionary<InfoHash, PeerWireConnection>();


        //Add a peer to the swarm
        public void AddToSwarm(InfoHash hash, PeerWireConnection connection)
        {
            //Hook into the peers upstream events

            //Handle piece events with the piece service
        }


        public void RemoveFromSwarm(InfoHash hash)
        {

        }
    }
}
