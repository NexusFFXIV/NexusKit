using NexusKit.Sync.Contracts;

namespace NexusKit.Sync.Protocol;

/// <summary>
/// Asks for everything that changed in a downlink collection since a cursor.
/// </summary>
/// <param name="ContractId">The contract the collection belongs to.</param>
/// <param name="Version">
/// The contract version this request was built against — the one the handshake negotiated.
/// See <see cref="PushRequest.Version"/> for why it is carried explicitly.
/// </param>
/// <param name="Collection">The downlink collection being read.</param>
/// <param name="Since">
/// The last sequence the client already holds; 0 for a first, full sync. The server returns
/// changes strictly after this.
/// <para>A cursor rather than a timestamp on purpose: clocks disagree, and a client whose
/// clock is two seconds fast would silently skip every record written in those two seconds.
/// A server-assigned sequence has no such failure mode.</para>
/// </param>
/// <param name="Limit">
/// Page size, or null for the server's default. Clamped to
/// <see cref="SyncLimits.MaxRecordsPerPull"/>.
/// </param>
public sealed record PullRequest(
    string ContractId,
    ContractVersion Version,
    string Collection,
    long Since,
    int? Limit = null);
