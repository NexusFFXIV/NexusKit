namespace NexusKit.Sync.Protocol;

/// <summary>
/// Operational bounds the server advertises at handshake, so a client can size its batches
/// instead of discovering the limits by being rejected.
/// </summary>
/// <param name="MaxRecordsPerPush">Largest batch the server will accept in one push.</param>
/// <param name="MaxPayloadBytes">Largest request body the server will accept.</param>
/// <param name="MaxRecordsPerPull">Largest page the server will return from a pull.</param>
public sealed record SyncLimits(
    int MaxRecordsPerPush,
    int MaxPayloadBytes,
    int MaxRecordsPerPull)
{
    /// <summary>
    /// Conservative fallback for a peer that advertised nothing. Deliberately small: guessing
    /// low costs an extra round trip, guessing high costs a rejected batch the client then has
    /// to split and resend anyway.
    /// </summary>
    public static SyncLimits Conservative { get; } = new(
        MaxRecordsPerPush: 100,
        MaxPayloadBytes: 1024 * 1024,
        MaxRecordsPerPull: 500);
}
