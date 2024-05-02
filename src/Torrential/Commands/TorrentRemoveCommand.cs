using Microsoft.EntityFrameworkCore;
using Torrential.Torrents;

namespace Torrential.Commands
{
    public class TorrentRemoveCommand : ICommand<TorrentRemoveResponse>
    {
        public required string InfoHash { get; init; }
    }

    public class TorrentRemoveResponse
    {

    }

    public class TorrentRemoveCommandHandler(TorrentTaskManager mgr, TorrentialDb db)
        : ICommandHandler<TorrentRemoveCommand, TorrentRemoveResponse>
    {
        public async Task<TorrentRemoveResponse> Execute(TorrentRemoveCommand command)
        {
            await mgr.Remove(command.InfoHash);
            await db.Torrents.Where(x => x.InfoHash == command.InfoHash).ExecuteDeleteAsync();
            return new() { };
        }
    }
}
