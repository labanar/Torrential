namespace Torrential.Application;

public interface ITcpListenerSettings
{
    bool Enabled { get; }
    int Port { get; }
}
