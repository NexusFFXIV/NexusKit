using System.Text.Json;

namespace NexusKit.Sync.Protocol;

/// <summary>
/// One record in a push batch — either an upsert or a delete.
/// </summary>
/// <param name="OpId">
/// Client-generated operation id, unique for the lifetime of the operation. This is what makes
/// a push idempotent: when a response is lost and the client retries, the server recognises the
/// id and does not apply the write twice. A ULID is the intended shape — sortable, so a server
/// can prune the dedupe window by age instead of keeping every id ever seen.
/// </param>
/// <param name="Key">
/// The record's key, matching the collection's declared key field. Two writes with the same key
/// are the same record.
/// </param>
/// <param name="Payload">
/// The record as a JSON object, or null for a delete.
/// <para>Implementations that retain this beyond the call must <see cref="JsonElement.Clone"/>
/// it — the element belongs to a <see cref="JsonDocument"/> the caller owns and may dispose.</para>
/// </param>
/// <param name="Deleted">True to tombstone the record rather than upsert it.</param>
public sealed record RecordWrite(
    string OpId,
    string Key,
    JsonElement? Payload,
    bool Deleted = false)
{
    /// <summary>Creates an upsert.</summary>
    public static RecordWrite Upsert(string opId, string key, JsonElement payload) =>
        new(opId, key, payload);

    /// <summary>Creates a tombstone.</summary>
    public static RecordWrite Delete(string opId, string key) =>
        new(opId, key, Payload: null, Deleted: true);
}
