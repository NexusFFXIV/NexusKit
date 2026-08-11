namespace NexusKit.Sync.Protocol;

/// <summary>What the server did with one record in a push batch.</summary>
public enum RecordWriteStatus
{
    /// <summary>Stored.</summary>
    Accepted,

    /// <summary>
    /// Already applied under this operation id — the retry of a push whose response was lost.
    /// Success from the client's point of view: the data is there, and the outbox entry can go.
    /// </summary>
    Duplicate,

    /// <summary>
    /// Refused. The reason is in the outcome's problems. Retrying unchanged will fail again,
    /// so a client should drop or quarantine the record rather than loop on it.
    /// </summary>
    Rejected,
}
