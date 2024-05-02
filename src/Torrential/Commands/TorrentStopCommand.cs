using Torrential.Torrents;

namespace Torrential.Commands
{
    public class TorrentStopCommand : ICommand<TorrentStopResponse>
    {
        public required string InfoHash { get; init; }
    }

    public class TorrentStopResponse
    {

    }

    public class TorrentStopCommandHandler(TorrentTaskManager mgr, TorrentialDb db)
        : ICommandHandler<TorrentStopCommand, TorrentStopResponse>
    {
        public async Task<TorrentStopResponse> Execute(TorrentStopCommand command)
        {
            await mgr.Stop(command.InfoHash);
            var torrent = await db.Torrents.FindAsync(command.InfoHash);
            if (torrent == null)
            {
                throw new ArgumentException("Torrent not found");
            }

            torrent.Status = TorrentStatus.Stopped;
            await db.SaveChangesAsync();
            return new() { };
        }
    }
}
