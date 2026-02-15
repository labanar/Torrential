using System.Net;

namespace Torrential.Core;

public readonly record struct PeerInfo(IPAddress Ip, int Port);
