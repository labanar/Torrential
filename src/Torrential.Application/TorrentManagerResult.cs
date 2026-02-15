namespace Torrential.Application;

public sealed class TorrentManagerResult
{
    public bool Success { get; init; }
    public TorrentManagerError? Error { get; init; }

    public static TorrentManagerResult Ok() => new() { Success = true };
    public static TorrentManagerResult Fail(TorrentManagerError error) => new() { Success = false, Error = error };
}

public enum TorrentManagerError
{
    TorrentAlreadyExists,
    TorrentNotFound,
    TorrentAlreadyRunning,
    TorrentAlreadyStopped,
    InvalidFileSelection
}
