namespace Torrential.Api;

internal sealed class DefaultTcpListenerSettings : Application.ITcpListenerSettings
{
    public bool Enabled => false;
    public int Port => 6881;
}
