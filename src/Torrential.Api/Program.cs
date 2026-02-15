using System.Text.Json.Serialization;
using Torrential.Api;
using Torrential.Application;
using Torrential.Core;
using Torrential.Core.Trackers;
using Torrential.Core.Trackers.Http;
using Torrential.Torrents;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ITcpListenerSettings, DefaultTcpListenerSettings>();
builder.Services.AddSingleton<HandshakeService>();
builder.Services.AddSingleton<ITrackerClient, HttpTrackerClient>();
builder.Services.AddHttpClient<HttpTrackerClient>();
builder.Services.AddTorrentApplication();
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);
builder.Services.AddAntiforgery();

var app = builder.Build();

var torrents = app.MapGroup("/torrents");

torrents.MapPost("/", (AddTorrentRequest request, ITorrentManager manager) =>
{
    var metaInfo = new TorrentMetaInfo
    {
        Name = request.Name,
        InfoHash = InfoHash.FromHexString(request.InfoHash),
        TotalSize = request.TotalSize,
        PieceSize = request.PieceSize,
        NumberOfPieces = request.NumberOfPieces,
        Files = request.Files.Select(f => new TorrentFileInfo(f.FileIndex, f.FileName, f.FileSize)).ToList(),
        AnnounceUrls = request.AnnounceUrls,
        PieceHashes = Convert.FromBase64String(request.PieceHashes)
    };

    var fileSelections = request.FileSelections?
        .Select(s => new TorrentFileSelection(s.FileIndex, s.Selected))
        .ToList();

    var result = manager.Add(metaInfo, fileSelections);
    return ToHttpResult(result);
});

torrents.MapPost("/parse", async (IFormFile file) =>
{
    using var stream = file.OpenReadStream();
    var metadata = TorrentMetadataParser.FromStream(stream);
    var coreInfoHash = new Torrential.Core.InfoHash(metadata.InfoHash.P1, metadata.InfoHash.P2, metadata.InfoHash.P3);
    return Results.Ok(new ParsedTorrentResponse(
        coreInfoHash.AsString(),
        metadata.Name,
        metadata.TotalSize,
        metadata.PieceSize,
        metadata.NumberOfPieces,
        metadata.Files.Select(f => new ParsedTorrentFileResponse((int)f.Id, f.Filename, f.FileSize)).ToList(),
        metadata.AnnounceList.ToList(),
        Convert.ToBase64String(metadata.PieceHashesConcatenated)
    ));
}).DisableAntiforgery();

torrents.MapPost("/add", async (IFormFile file, ITorrentManager manager) =>
{
    using var stream = file.OpenReadStream();
    var metadata = TorrentMetadataParser.FromStream(stream);
    var coreInfoHash = new Torrential.Core.InfoHash(metadata.InfoHash.P1, metadata.InfoHash.P2, metadata.InfoHash.P3);
    var metaInfo = new TorrentMetaInfo
    {
        InfoHash = coreInfoHash,
        Name = metadata.Name,
        TotalSize = metadata.TotalSize,
        PieceSize = metadata.PieceSize,
        NumberOfPieces = metadata.NumberOfPieces,
        Files = metadata.Files.Select(f => new TorrentFileInfo((int)f.Id, f.Filename, f.FileSize)).ToList(),
        AnnounceUrls = metadata.AnnounceList.ToList(),
        PieceHashes = metadata.PieceHashesConcatenated
    };

    var result = manager.Add(metaInfo);
    return ToHttpResult(result);
}).DisableAntiforgery();

torrents.MapPost("/{infoHash}/start", (InfoHash infoHash, ITorrentManager manager) =>
    ToHttpResult(manager.Start(infoHash)));

torrents.MapPost("/{infoHash}/stop", (InfoHash infoHash, ITorrentManager manager) =>
    ToHttpResult(manager.Stop(infoHash)));

torrents.MapDelete("/{infoHash}", (InfoHash infoHash, bool? deleteData, ITorrentManager manager) =>
    ToHttpResult(manager.Remove(infoHash, deleteData ?? false)));

torrents.MapGet("/", (ITorrentManager manager) =>
    Results.Ok(manager.GetAll().Select(ToDto).ToList()));

torrents.MapGet("/{infoHash}", (InfoHash infoHash, ITorrentManager manager) =>
{
    var state = manager.GetState(infoHash);
    return state is not null ? Results.Ok(ToDto(state)) : Results.NotFound();
});

torrents.MapGet("/{infoHash}/details", (InfoHash infoHash, ITorrentManager manager) =>
{
    var state = manager.GetState(infoHash);
    if (state is null) return Results.NotFound();

    var status = state.Status switch
    {
        TorrentStatus.Downloading => "Running",
        TorrentStatus.Stopped => "Stopped",
        _ => "Idle"
    };

    var details = new TorrentDetailsDto(
        state.InfoHash.AsString(),
        state.Name,
        state.TotalSize,
        state.PieceSize,
        state.NumberOfPieces,
        status,
        state.DateAdded,
        new bool[state.NumberOfPieces],
        state.Files.Select(f => new TorrentFileDto(f.FileIndex, f.FileName, f.FileSize, state.SelectedFileIndices.Contains(f.FileIndex))).ToList(),
        []
    );

    return Results.Ok(details);
});

torrents.MapPost("/{infoHash}/files", (InfoHash infoHash, UpdateFileSelectionsRequest request, ITorrentManager manager) =>
{
    var selections = request.FileSelections
        .Select(s => new TorrentFileSelection(s.FileIndex, s.Selected))
        .ToList();

    return ToHttpResult(manager.UpdateFileSelections(infoHash, selections));
});

app.Run();

static IResult ToHttpResult(TorrentManagerResult result)
{
    if (result.Success)
        return Results.Ok(new { success = true });

    return result.Error switch
    {
        TorrentManagerError.TorrentNotFound => Results.NotFound(new { error = result.Error.ToString() }),
        TorrentManagerError.TorrentAlreadyExists or
        TorrentManagerError.TorrentAlreadyRunning or
        TorrentManagerError.TorrentAlreadyStopped => Results.Conflict(new { error = result.Error.ToString() }),
        _ => Results.BadRequest(new { error = result.Error.ToString() })
    };
}

static TorrentDto ToDto(TorrentState state)
{
    var status = state.Status switch
    {
        TorrentStatus.Downloading => "Running",
        TorrentStatus.Stopped => "Stopped",
        _ => "Idle"
    };

    return new TorrentDto(
        state.InfoHash.AsString(),
        state.Name,
        0,
        0,
        0,
        0,
        0,
        state.TotalSize,
        [],
        status
    );
}

public record TorrentDto(
    string InfoHash,
    string Name,
    double Progress,
    long BytesDownloaded,
    long BytesUploaded,
    double DownloadRate,
    double UploadRate,
    long TotalSizeBytes,
    List<object> Peers,
    string Status);

public record ParsedTorrentFileResponse(int FileIndex, string FileName, long FileSize);
public record ParsedTorrentResponse(
    string InfoHash,
    string Name,
    long TotalSize,
    long PieceSize,
    int NumberOfPieces,
    List<ParsedTorrentFileResponse> Files,
    List<string> AnnounceUrls,
    string PieceHashes);

public record AddTorrentFileRequest(int FileIndex, string FileName, long FileSize);
public record AddTorrentFileSelectionRequest(int FileIndex, bool Selected);
public record AddTorrentRequest(
    string Name,
    string InfoHash,
    long TotalSize,
    long PieceSize,
    int NumberOfPieces,
    List<AddTorrentFileRequest> Files,
    List<string> AnnounceUrls,
    string PieceHashes,
    List<AddTorrentFileSelectionRequest>? FileSelections = null);

public record TorrentDetailsDto(
    string InfoHash,
    string Name,
    long TotalSizeBytes,
    long PieceSize,
    int NumberOfPieces,
    string Status,
    DateTimeOffset DateAdded,
    bool[] Pieces,
    List<TorrentFileDto> Files,
    List<PeerDetailDto> Peers);

public record TorrentFileDto(int FileIndex, string FileName, long FileSize, bool Selected);

public record PeerDetailDto(
    string PeerId,
    string IpAddress,
    int Port,
    long BytesDownloaded,
    long BytesUploaded,
    bool IsSeed,
    double Progress,
    bool[] Pieces);

public record UpdateFileSelectionItem(int FileIndex, bool Selected);
public record UpdateFileSelectionsRequest(List<UpdateFileSelectionItem> FileSelections);
