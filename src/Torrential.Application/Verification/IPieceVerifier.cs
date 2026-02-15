namespace Torrential.Application.Verification;

public interface IPieceVerifier
{
    Task VerifyPieceAsync(InfoHash infoHash, int pieceIndex, CancellationToken cancellationToken = default);
}
