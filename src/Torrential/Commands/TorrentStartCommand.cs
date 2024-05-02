﻿using Torrential.Torrents;

namespace Torrential.Commands
{
    public class TorrentStartCommand : ICommand<TorrentStartResponse>
    {
        public required string InfoHash { get; init; }
    }

    public class TorrentStartResponse
    {

    }

    public class TorrentStartCommandHandler(TorrentTaskManager mgr, TorrentialDb db)
        : ICommandHandler<TorrentStartCommand, TorrentStartResponse>
    {
        public async Task<TorrentStartResponse> Execute(TorrentStartCommand command)
        {

            var torrent = await db.Torrents.FindAsync(command.InfoHash);
            if (torrent == null)
            {
                throw new ArgumentException("Torrent not found");
            }
            await mgr.Start(command.InfoHash);

            torrent.Status = TorrentStatus.Running;
            await db.SaveChangesAsync();
            return new() { };
        }
    }
}
