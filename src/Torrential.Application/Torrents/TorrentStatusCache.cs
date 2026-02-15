using System.Collections.Concurrent;
using Torrential.Application.Data;

namespace Torrential.Application.Torrents;

public class TorrentStatusCache
{
    private readonly ConcurrentDictionary<string, TorrentStatus> _statuses = new();
    public ValueTask<TorrentStatus> GetStatus(InfoHash infoHash)
    {
        if (!_statuses.TryGetValue(infoHash, out var status))
            return new ValueTask<TorrentStatus>(TorrentStatus.Idle);

        return new ValueTask<TorrentStatus>(status);
    }

    public void UpdateStatus(InfoHash infoHash, TorrentStatus status)
    {
        _statuses.AddOrUpdate(infoHash, status, (_, _) => status);
    }

    public void RemoveStatus(InfoHash infoHash)
    {
        _statuses.TryRemove(infoHash, out _);
    }
}
