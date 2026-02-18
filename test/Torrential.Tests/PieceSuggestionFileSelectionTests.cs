using Torrential.Peers;

namespace Torrential.Tests;

/// <summary>
/// Tests that the piece suggestion algorithm correctly filters by allowed-pieces mask.
/// These verify that unselected-file pieces are never chosen.
/// </summary>
public class PieceSuggestionFileSelectionTests
{
    [Fact]
    public void AllowedMask_OnlyAllowedPiecesAreSuggested()
    {
        // 8 pieces: peer has all, we have none.
        // Only pieces 0-3 are allowed (file selection).
        byte[] ourBytes = [0x00]; // we have nothing
        byte[] peerBytes = [0xFF]; // peer has all 8
        byte[] reservationBytes = [0x00];
        byte[] allowedBytes = [0xF0]; // bits 7-4 set → pieces 0-3 allowed

        var result = PieceSuggestion.SuggestPiece(
            ourBytes, peerBytes, reservationBytes,
            ReadOnlySpan<int>.Empty, allowedBytes, 8);

        Assert.NotNull(result.Index);
        Assert.InRange(result.Index.Value, 0, 3);
    }

    [Fact]
    public void AllowedMask_Empty_AllPiecesConsidered()
    {
        // When allowedBytes is empty, all pieces should be candidates.
        byte[] ourBytes = [0x00];
        byte[] peerBytes = [0xFF];
        byte[] reservationBytes = [0x00];

        var result = PieceSuggestion.SuggestPiece(
            ourBytes, peerBytes, reservationBytes,
            ReadOnlySpan<int>.Empty, ReadOnlySpan<byte>.Empty, 8);

        Assert.NotNull(result.Index);
        Assert.InRange(result.Index.Value, 0, 7);
    }

    [Fact]
    public void AllowedMask_AllZero_NoPieceSuggested()
    {
        // Allowed mask blocks everything.
        byte[] ourBytes = [0x00];
        byte[] peerBytes = [0xFF];
        byte[] reservationBytes = [0x00];
        byte[] allowedBytes = [0x00]; // nothing allowed

        var result = PieceSuggestion.SuggestPiece(
            ourBytes, peerBytes, reservationBytes,
            ReadOnlySpan<int>.Empty, allowedBytes, 8);

        Assert.Null(result.Index);
    }

    [Fact]
    public void AllowedMask_SinglePiece_ReturnsThatPiece()
    {
        // Only piece 5 is allowed.
        byte[] ourBytes = [0x00];
        byte[] peerBytes = [0xFF];
        byte[] reservationBytes = [0x00];
        // Piece 5 = bit index (7-5)=2 in byte 0 → 0b00000100 = 0x04
        byte[] allowedBytes = [0x04];

        var result = PieceSuggestion.SuggestPiece(
            ourBytes, peerBytes, reservationBytes,
            ReadOnlySpan<int>.Empty, allowedBytes, 8);

        Assert.NotNull(result.Index);
        Assert.Equal(5, result.Index.Value);
    }

    [Fact]
    public void AllowedMask_PeerDoesNotHaveAllowedPiece_NoPieceSuggested()
    {
        // Piece 0 is the only allowed piece, but the peer doesn't have it.
        byte[] ourBytes = [0x00];
        byte[] peerBytes = [0x7F]; // peer has pieces 1-7, not piece 0
        byte[] reservationBytes = [0x00];
        byte[] allowedBytes = [0x80]; // only piece 0 allowed

        var result = PieceSuggestion.SuggestPiece(
            ourBytes, peerBytes, reservationBytes,
            ReadOnlySpan<int>.Empty, allowedBytes, 8);

        Assert.Null(result.Index);
    }

    [Fact]
    public void AllowedMask_WeAlreadyHaveAllowedPieces_NoPieceSuggested()
    {
        // Pieces 0-3 are allowed, but we already have them all.
        byte[] ourBytes = [0xF0]; // we have pieces 0-3
        byte[] peerBytes = [0xFF]; // peer has all
        byte[] reservationBytes = [0x00];
        byte[] allowedBytes = [0xF0]; // pieces 0-3 allowed

        var result = PieceSuggestion.SuggestPiece(
            ourBytes, peerBytes, reservationBytes,
            ReadOnlySpan<int>.Empty, allowedBytes, 8);

        Assert.Null(result.Index);
    }

    [Fact]
    public void AllowedMask_ReservedPiecesSkipped()
    {
        // Pieces 0-3 allowed. Pieces 0-1 reserved. Should suggest piece 2 or 3.
        byte[] ourBytes = [0x00];
        byte[] peerBytes = [0xFF];
        byte[] reservationBytes = [0xC0]; // pieces 0-1 reserved
        byte[] allowedBytes = [0xF0]; // pieces 0-3 allowed

        var result = PieceSuggestion.SuggestPiece(
            ourBytes, peerBytes, reservationBytes,
            ReadOnlySpan<int>.Empty, allowedBytes, 8);

        Assert.NotNull(result.Index);
        Assert.InRange(result.Index.Value, 2, 3);
    }

    [Fact]
    public void AllowedMask_MultipleByte_CorrectPiecesSelected()
    {
        // 16 pieces across 2 bytes. Allow only pieces 8-15 (second byte).
        byte[] ourBytes = [0x00, 0x00];
        byte[] peerBytes = [0xFF, 0xFF];
        byte[] reservationBytes = [0x00, 0x00];
        byte[] allowedBytes = [0x00, 0xFF]; // only second byte allowed

        var result = PieceSuggestion.SuggestPiece(
            ourBytes, peerBytes, reservationBytes,
            ReadOnlySpan<int>.Empty, allowedBytes, 16);

        Assert.NotNull(result.Index);
        Assert.InRange(result.Index.Value, 8, 15);
    }

    [Fact]
    public void Bitfield_SuggestPieceToDownload_WithAllowedMask()
    {
        // Test through the Bitfield class method (not just the static helper).
        // 8 pieces: peer has all, we have none. Only piece 7 allowed.
        var myBitfield = new Bitfield(8);
        var peerData = new byte[] { 0xFF };
        var peerBitfield = new Bitfield(peerData.AsSpan());
        // Piece 7 = bit index (7-7)=0 → 0b00000001 = 0x01
        var allowedBitfield = new Bitfield(8);
        allowedBitfield.MarkHave(7);

        var result = myBitfield.SuggestPieceToDownload(peerBitfield, null, null, allowedBitfield);

        Assert.NotNull(result.Index);
        Assert.Equal(7, result.Index.Value);
    }

    [Fact]
    public void Bitfield_SuggestPieceToDownload_NullAllowed_AllPiecesConsidered()
    {
        // When allowedPieces is null, all pieces are fair game.
        var myBitfield = new Bitfield(8);
        var peerData = new byte[] { 0xFF };
        var peerBitfield = new Bitfield(peerData.AsSpan());

        var result = myBitfield.SuggestPieceToDownload(peerBitfield, null, null, null);

        Assert.NotNull(result.Index);
        Assert.InRange(result.Index.Value, 0, 7);
    }

    [Fact]
    public void AllowedMask_RarestFirst_PicksRarestAmongAllowed()
    {
        // 8 pieces. Allowed: pieces 2, 3, 4. Piece 4 is rarest (availability=1).
        byte[] ourBytes = [0x00];
        byte[] peerBytes = [0xFF];
        byte[] reservationBytes = [0x00];
        // Pieces 2,3,4 → bits 5,4,3 → 0b00111000 = 0x38
        byte[] allowedBytes = [0x38];
        int[] availability = [10, 10, 5, 5, 1, 10, 10, 10];

        // Run multiple times to confirm piece 4 is always picked (it's uniquely rarest).
        for (int trial = 0; trial < 10; trial++)
        {
            var result = PieceSuggestion.SuggestPiece(
                ourBytes, peerBytes, reservationBytes,
                availability, allowedBytes, 8);

            Assert.NotNull(result.Index);
            Assert.Equal(4, result.Index.Value);
        }
    }
}
