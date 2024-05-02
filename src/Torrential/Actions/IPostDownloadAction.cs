namespace Torrential.Pipelines
{

    /// <summary>
    /// Upon a torrent completing we can perform some action
    /// Examples: Move the file to a different location, notify the user, etc
    /// </summary>
    public interface IPostDownloadAction
    {
        string Name { get; }
        Task<PostDownloadActionResult> ExecuteAsync(InfoHash infoHash, CancellationToken cancellationToken);
        bool ContinueOnFailure { get; }
    }


    public class PostDownloadActionResult
    {
        public required bool Success { get; init; }
    }
}
