namespace NexusKit.Sync.Protocol;

/// <summary>
/// A Problem Details payload (RFC 9457) as this protocol uses it.
/// </summary>
/// <param name="Type">Stable identifier — see <see cref="SyncProblemType"/>. Branch on this, not on <paramref name="Detail"/>.</param>
/// <param name="Title">Short human-readable summary.</param>
/// <param name="Status">The HTTP status that carried it.</param>
/// <param name="Detail">Human-readable specifics. Free text; never parse it.</param>
/// <param name="Extensions">
/// Type-specific fields. A contract mismatch carries the versions the server knows, a
/// validation failure carries the per-field problems, and so on — which is what lets a client
/// react usefully instead of only reporting that something went wrong.
/// </param>
public sealed record SyncProblem(
    string Type,
    string Title,
    int Status,
    string? Detail = null,
    IReadOnlyDictionary<string, string>? Extensions = null)
{
    /// <inheritdoc />
    public override string ToString() =>
        Detail is null ? $"{Status} {Title} ({Type})" : $"{Status} {Title} ({Type}): {Detail}";
}
