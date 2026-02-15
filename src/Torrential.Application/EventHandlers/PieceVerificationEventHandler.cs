using Torrential.Application.Events;
using Torrential.Application.Verification;

namespace Torrential.Application.EventHandlers;

public class PieceVerificationEventHandler(IPieceVerifier pieceVerifier)
    : IEventHandler<PieceValidationRequest>
{
    public async Task HandleAsync(PieceValidationRequest @event, CancellationToken cancellationToken = default)
    {
        await pieceVerifier.VerifyPieceAsync(@event.InfoHash, @event.PieceIndex, cancellationToken);
    }
}
