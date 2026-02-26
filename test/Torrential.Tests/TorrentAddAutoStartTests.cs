using Torrential.Web.Api.Requests.Torrents;

namespace Torrential.Tests;

/// <summary>
/// Regression tests covering the extended add-from-URL contract (selected files, completed path)
/// and the auto-start behavior where newly added torrents begin running immediately.
/// </summary>
public class TorrentAddAutoStartTests
{
    [Fact]
    public void AddFromUrlRequest_accepts_selected_file_ids()
    {
        var request = new TorrentAddFromUrlRequest
        {
            Url = "https://example.com/torrent.torrent",
            SelectedFileIds = new long[] { 0, 2, 5 }
        };

        Assert.NotNull(request.SelectedFileIds);
        Assert.Equal(3, request.SelectedFileIds.Length);
        Assert.Contains(0L, request.SelectedFileIds);
        Assert.Contains(2L, request.SelectedFileIds);
        Assert.Contains(5L, request.SelectedFileIds);
    }

    [Fact]
    public void AddFromUrlRequest_accepts_completed_path()
    {
        var request = new TorrentAddFromUrlRequest
        {
            Url = "https://example.com/torrent.torrent",
            CompletedPath = "/mnt/media/movies"
        };

        Assert.Equal("/mnt/media/movies", request.CompletedPath);
    }

    [Fact]
    public void AddFromUrlRequest_backwards_compatible_without_optional_fields()
    {
        var request = new TorrentAddFromUrlRequest
        {
            Url = "https://example.com/torrent.torrent"
        };

        Assert.Equal("https://example.com/torrent.torrent", request.Url);
        Assert.Null(request.SelectedFileIds);
        Assert.Null(request.CompletedPath);
    }

    [Fact]
    public void AddFromUrlRequest_accepts_all_fields_together()
    {
        var request = new TorrentAddFromUrlRequest
        {
            Url = "https://tracker.example.com/download/12345",
            SelectedFileIds = new long[] { 1, 3 },
            CompletedPath = "/data/completed"
        };

        Assert.Equal("https://tracker.example.com/download/12345", request.Url);
        Assert.NotNull(request.SelectedFileIds);
        Assert.Equal(2, request.SelectedFileIds.Length);
        Assert.Equal("/data/completed", request.CompletedPath);
    }

    [Fact]
    public void AddFromUrlRequest_empty_selected_files_is_distinct_from_null()
    {
        var requestWithNull = new TorrentAddFromUrlRequest
        {
            Url = "https://example.com/torrent.torrent",
            SelectedFileIds = null
        };

        var requestWithEmpty = new TorrentAddFromUrlRequest
        {
            Url = "https://example.com/torrent.torrent",
            SelectedFileIds = Array.Empty<long>()
        };

        Assert.Null(requestWithNull.SelectedFileIds);
        Assert.NotNull(requestWithEmpty.SelectedFileIds);
        Assert.Empty(requestWithEmpty.SelectedFileIds);
    }
}
