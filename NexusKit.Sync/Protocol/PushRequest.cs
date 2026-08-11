using NexusKit.Sync.Contracts;

namespace NexusKit.Sync.Protocol;

/// <summary>
/// A batch of writes for one uplink collection.
/// </summary>
/// <param name="ContractId">The contract the collection belongs to.</param>
/// <param name="Version">
/// The contract version this batch was built against — the one the handshake negotiated.
/// <para>Carried explicitly because majors coexist on a server. Without it a server holding
/// both 1.x and 2.x could not tell which rules to validate against, and would have to guess;
/// guessing wrong means either rejecting valid data or accepting data the client's contract
/// forbids. Within a major the server may validate against a higher minor, which is safe
/// precisely because minors may only loosen constraints.</para>
/// </param>
/// <param name="Collection">The uplink collection being written.</param>
/// <param name="Records">
/// The batch. Keep it within the server's advertised
/// <see cref="SyncLimits.MaxRecordsPerPush"/>; larger batches are rejected whole rather than
/// truncated, because a partially-applied batch the client believes was applied entirely is
/// the worse failure.
/// </param>
public sealed record PushRequest(
    string ContractId,
    ContractVersion Version,
    string Collection,
    IReadOnlyList<RecordWrite> Records);
