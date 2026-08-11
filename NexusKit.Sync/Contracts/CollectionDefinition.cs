namespace NexusKit.Sync.Contracts;

/// <summary>
/// One named dataset within a contract — the contract equivalent of a table or a topic.
/// </summary>
public sealed record CollectionDefinition
{
    /// <summary>Collection name, unique within the contract. See <see cref="ContractNames"/>.</summary>
    public required string Name { get; init; }

    /// <summary>Which way this dataset flows. See <see cref="SyncDirection"/>.</summary>
    public required SyncDirection Direction { get; init; }

    /// <summary>
    /// Name of the field that identifies a record. Must name a declared, required field.
    /// <para>The key is what makes a push idempotent and a tombstone meaningful: two writes
    /// carrying the same key are the same record, so a client that retries after a dropped
    /// response updates rather than duplicates.</para>
    /// </summary>
    public required string Key { get; init; }

    /// <summary>The fields a record may carry. Order is not significant.</summary>
    public required IReadOnlyList<FieldDefinition> Fields { get; init; }

    /// <summary>
    /// Names of fields the server should index for querying. Order is not significant.
    /// <para>Records are stored generically so contracts can be registered at runtime, which
    /// means there is no migration in which someone could hand-write an index. Declaring one
    /// here is how a collection gets targeted query performance without anyone touching the
    /// server.</para>
    /// </summary>
    public IReadOnlyList<string> Indexed { get; init; } = [];

    /// <summary>Per-key write budget, or null for the server's default.</summary>
    public RateLimitPolicy? RateLimit { get; init; }

    /// <summary>
    /// How long records live before the server prunes them, or null to keep them
    /// indefinitely. Declaring a retention is the cheapest privacy control available:
    /// data that has been deleted cannot leak.
    /// </summary>
    public TimeSpan? Retention { get; init; }

    /// <summary>
    /// Marks the collection as a candidate for server-initiated push over a live channel.
    /// Reserved: the model carries it so contracts written today stay valid once the live
    /// channel exists, but v1 implementations poll and may ignore it.
    /// </summary>
    public bool Live { get; init; }

    /// <summary>Finds a field by name, or null when the collection does not declare it.</summary>
    public FieldDefinition? FindField(string name)
    {
        // Linear scan on purpose: collections have a handful of fields, and a dictionary
        // per collection would cost more in allocation than it saves in lookups.
        foreach (var field in Fields)
        {
            if (string.Equals(field.Name, name, StringComparison.Ordinal)) return field;
        }

        return null;
    }

    /// <summary>The scope string a caller needs to use this collection, e.g. <c>reports:push</c>.</summary>
    public string Scope => ContractScopes.For(this);
}
