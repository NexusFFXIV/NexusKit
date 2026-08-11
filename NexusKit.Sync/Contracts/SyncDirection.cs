namespace NexusKit.Sync.Contracts;

/// <summary>
/// Which way a <see cref="CollectionDefinition"/> flows.
/// <para>Direction is a property of the collection, not of an individual call: uplink and
/// downlink are separate datasets, not two sides of the same one. A contract may declare
/// three uplinks and one downlink, and nothing has to correspond between them.</para>
/// <para>There is deliberately no bidirectional value. Allowing the same collection to be
/// written from both ends forces conflict resolution into every implementation, and the
/// cases that genuinely need it are rare enough to model as two collections.</para>
/// </summary>
public enum SyncDirection
{
    /// <summary>Client to server — what a plugin collects while the game runs.</summary>
    Uplink,

    /// <summary>
    /// Server to client — what an author curates outside the game, typically through a web
    /// interface, and which clients mirror locally.
    /// </summary>
    Downlink,
}
