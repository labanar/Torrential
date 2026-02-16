using System.Threading.Channels;
using Torrential.Files;
using Torrential.Peers;

namespace Torrential.Torrents;

/// <summary>
/// Lightweight in-process event bus that replaces MassTransit for all internal event dispatch.
///
/// Design rationale:
///   - MassTransit's in-memory transport allocates ConsumeContext&lt;T&gt; wrappers, middleware
///     pipeline objects, logging strings, and async state machines per Publish call.
///     On the hot path (per-piece validation, per-piece verified broadcast) this produces
///     thousands of gen0/gen1 allocations per second during fast downloads.
///   - This bus dispatches directly to handler delegates with zero intermediate objects.
///   - Hot-path events (PieceValidationRequest) use a bounded Channel for backpressure
///     and sequential processing, matching MassTransit's consumer concurrency=1 default.
///   - Cold-path events (TorrentAdded, etc.) dispatch inline on the caller's thread.
///   - All handlers are registered once at startup. No dictionary lookups per Publish.
///
/// Thread safety:
///   - All handler lists are set once during DI composition and are read-only thereafter.
///   - Channel operations are thread-safe by design.
/// </summary>
public sealed class TorrentEventBus : IAsyncDisposable
{
    // ---------------------------------------------------------------------------
    // Hot-path: PieceValidationRequest goes through a channel for sequential processing
    // ---------------------------------------------------------------------------
    private readonly Channel<PieceValidationRequest> _validationChannel = Channel.CreateBounded<PieceValidationRequest>(
        new BoundedChannelOptions(64)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

    private Func<PieceValidationRequest, Task>? _validationHandler;
    private Task? _validationProcessingTask;
    private CancellationTokenSource? _cts;

    // ---------------------------------------------------------------------------
    // Event handlers - set once at startup, invoked directly on publish
    // ---------------------------------------------------------------------------

    // Hot path
    private readonly List<Func<TorrentPieceVerifiedEvent, Task>> _pieceVerifiedHandlers = [];
    private readonly List<Func<TorrentCompleteEvent, Task>> _completeHandlers = [];
    private readonly List<Func<TorrentPieceDownloadedEvent, Task>> _pieceDownloadedHandlers = [];

    // Cold path - lifecycle
    private readonly List<Func<TorrentAddedEvent, Task>> _addedHandlers = [];
    private readonly List<Func<TorrentStartedEvent, Task>> _startedHandlers = [];
    private readonly List<Func<TorrentStoppedEvent, Task>> _stoppedHandlers = [];
    private readonly List<Func<TorrentRemovedEvent, Task>> _removedHandlers = [];

    // Cold path - peer events
    private readonly List<Func<PeerConnectedEvent, Task>> _peerConnectedHandlers = [];
    private readonly List<Func<PeerDisconnectedEvent, Task>> _peerDisconnectedHandlers = [];
    private readonly List<Func<PeerBitfieldReceivedEvent, Task>> _peerBitfieldHandlers = [];

    // Periodic stats
    private readonly List<Func<TorrentStatsEvent, Task>> _statsHandlers = [];

    // File copy (currently no consumers, but wired for forward compatibility)
    private readonly List<Func<TorrentFileCopyStartedEvent, Task>> _fileCopyStartedHandlers = [];
    private readonly List<Func<TorrentFileCopyCompletedEvent, Task>> _fileCopyCompletedHandlers = [];

    // File selection
    private readonly List<Func<FileSelectionChangedEvent, Task>> _fileSelectionChangedHandlers = [];

    // ---------------------------------------------------------------------------
    // Registration (called during DI setup, before any Publish)
    // ---------------------------------------------------------------------------

    public void OnPieceValidationRequest(Func<PieceValidationRequest, Task> handler)
    {
        _validationHandler = handler;
    }

    public void OnPieceVerified(Func<TorrentPieceVerifiedEvent, Task> handler) => _pieceVerifiedHandlers.Add(handler);
    public void OnTorrentComplete(Func<TorrentCompleteEvent, Task> handler) => _completeHandlers.Add(handler);
    public void OnPieceDownloaded(Func<TorrentPieceDownloadedEvent, Task> handler) => _pieceDownloadedHandlers.Add(handler);

    public void OnTorrentAdded(Func<TorrentAddedEvent, Task> handler) => _addedHandlers.Add(handler);
    public void OnTorrentStarted(Func<TorrentStartedEvent, Task> handler) => _startedHandlers.Add(handler);
    public void OnTorrentStopped(Func<TorrentStoppedEvent, Task> handler) => _stoppedHandlers.Add(handler);
    public void OnTorrentRemoved(Func<TorrentRemovedEvent, Task> handler) => _removedHandlers.Add(handler);

    public void OnPeerConnected(Func<PeerConnectedEvent, Task> handler) => _peerConnectedHandlers.Add(handler);
    public void OnPeerDisconnected(Func<PeerDisconnectedEvent, Task> handler) => _peerDisconnectedHandlers.Add(handler);
    public void OnPeerBitfieldReceived(Func<PeerBitfieldReceivedEvent, Task> handler) => _peerBitfieldHandlers.Add(handler);

    public void OnTorrentStats(Func<TorrentStatsEvent, Task> handler) => _statsHandlers.Add(handler);

    public void OnFileCopyStarted(Func<TorrentFileCopyStartedEvent, Task> handler) => _fileCopyStartedHandlers.Add(handler);
    public void OnFileCopyCompleted(Func<TorrentFileCopyCompletedEvent, Task> handler) => _fileCopyCompletedHandlers.Add(handler);

    public void OnFileSelectionChanged(Func<FileSelectionChangedEvent, Task> handler) => _fileSelectionChangedHandlers.Add(handler);

    /// <summary>
    /// Starts the background channel reader for PieceValidationRequest.
    /// Must be called after all handlers are registered (typically from a hosted service or startup).
    /// </summary>
    public void Start(CancellationToken stoppingToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        _validationProcessingTask = ProcessValidationChannel(_cts.Token);
    }

    // ---------------------------------------------------------------------------
    // Publish methods - zero allocation beyond the event struct/class itself
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Queues a piece validation request into the bounded channel.
    /// Backpressure: if the channel is full (64 items), the caller awaits.
    /// </summary>
    public ValueTask PublishPieceValidationRequest(PieceValidationRequest request)
    {
        return _validationChannel.Writer.WriteAsync(request);
    }

    public Task PublishPieceVerified(TorrentPieceVerifiedEvent evt) => InvokeAll(_pieceVerifiedHandlers, evt);
    public Task PublishTorrentComplete(TorrentCompleteEvent evt) => InvokeAll(_completeHandlers, evt);
    public Task PublishPieceDownloaded(TorrentPieceDownloadedEvent evt) => InvokeAll(_pieceDownloadedHandlers, evt);

    public Task PublishTorrentAdded(TorrentAddedEvent evt) => InvokeAll(_addedHandlers, evt);
    public Task PublishTorrentStarted(TorrentStartedEvent evt) => InvokeAll(_startedHandlers, evt);
    public Task PublishTorrentStopped(TorrentStoppedEvent evt) => InvokeAll(_stoppedHandlers, evt);
    public Task PublishTorrentRemoved(TorrentRemovedEvent evt) => InvokeAll(_removedHandlers, evt);

    public Task PublishPeerConnected(PeerConnectedEvent evt) => InvokeAll(_peerConnectedHandlers, evt);
    public Task PublishPeerDisconnected(PeerDisconnectedEvent evt) => InvokeAll(_peerDisconnectedHandlers, evt);
    public Task PublishPeerBitfieldReceived(PeerBitfieldReceivedEvent evt) => InvokeAll(_peerBitfieldHandlers, evt);

    public Task PublishTorrentStats(TorrentStatsEvent evt) => InvokeAll(_statsHandlers, evt);

    public Task PublishFileCopyStarted(TorrentFileCopyStartedEvent evt) => InvokeAll(_fileCopyStartedHandlers, evt);
    public Task PublishFileCopyCompleted(TorrentFileCopyCompletedEvent evt) => InvokeAll(_fileCopyCompletedHandlers, evt);

    public Task PublishFileSelectionChanged(FileSelectionChangedEvent evt) => InvokeAll(_fileSelectionChangedHandlers, evt);

    // ---------------------------------------------------------------------------
    // Internal
    // ---------------------------------------------------------------------------

    private async Task ProcessValidationChannel(CancellationToken ct)
    {
        await foreach (var request in _validationChannel.Reader.ReadAllAsync(ct))
        {
            if (_validationHandler != null)
            {
                try
                {
                    await _validationHandler(request);
                }
                catch
                {
                    // Validation failures are logged inside the handler.
                    // Swallow here to keep the channel reader alive.
                }
            }
        }
    }

    private static Task InvokeAll<T>(List<Func<T, Task>> handlers, T evt)
    {
        // Fast path: no handlers registered
        if (handlers.Count == 0)
            return Task.CompletedTask;

        // Fast path: single handler (most common case) — no Task[] allocation
        if (handlers.Count == 1)
            return handlers[0](evt);

        // Multiple handlers: invoke sequentially to avoid Task.WhenAll array allocation.
        // All handlers are lightweight (SignalR send, dictionary write, etc.) so
        // sequential dispatch adds negligible latency vs the allocation saved.
        return InvokeAllSequential(handlers, evt);
    }

    private static async Task InvokeAllSequential<T>(List<Func<T, Task>> handlers, T evt)
    {
        for (int i = 0; i < handlers.Count; i++)
            await handlers[i](evt);
    }

    public async ValueTask DisposeAsync()
    {
        _validationChannel.Writer.TryComplete();
        if (_cts != null)
        {
            await _cts.CancelAsync();
            _cts.Dispose();
        }

        if (_validationProcessingTask != null)
        {
            try { await _validationProcessingTask; }
            catch (OperationCanceledException) { }
        }
    }
}
