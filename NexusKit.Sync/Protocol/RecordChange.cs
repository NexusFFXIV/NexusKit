using System.Text.Json;

namespace NexusKit.Sync.Protocol;

/// <summary>
/// One change in a pull page — an upsert or a tombstone.
/// </summary>
/// <param name="Key">The record's key.</param>
/// <param name="Payload">
/// The record as a JSON object, or null when <paramref name="Deleted"/> is true.
/// <para>Clone it if you retain it past the call; it belongs to a document the caller owns.</para>
/// </param>
/// <param name="Deleted">
/// True when the record was removed. Tombstones are carried explicitly rather than simply
/// omitting the record, because a client mirroring the collection has no other way to learn
/// that something it already holds is gone.
/// </param>
/// <param name="Sequence">
/// The server-assigned sequence for this change. Monotonic within a collection; the client
/// stores the highest it has seen as its next cursor.
/// </param>
/// <param name="Revision">
/// Per-record revision counter, incremented on each write. Unused by v1's read-only downlink
/// flow, and carried anyway so that adding conflict detection later does not need a wire change.
/// </param>
/// <param name="UpdatedAt">When the server applied the change. Informational — ordering comes from <paramref name="Sequence"/>.</param>
public sealed record RecordChange(
    string Key,
    JsonElement? Payload,
    bool Deleted,
    long Sequence,
    int Revision,
    DateTimeOffset UpdatedAt);
