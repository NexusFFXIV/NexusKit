namespace NexusKit.Sync.Protocol;

/// <summary>
/// What the server made of a push batch — one outcome per submitted record.
/// </summary>
/// <param name="Outcomes">
/// In no guaranteed order; match them to submitted records by operation id rather than by
/// position, so that a server free to reorder or coalesce internally stays interoperable.
/// </param>
public sealed record PushResult(IReadOnlyList<RecordOutcome> Outcomes)
{
    /// <summary>Records the client may drop from its outbox — stored or already-stored.</summary>
    public IEnumerable<RecordOutcome> Settled => Outcomes.Where(o => o.IsSettled);

    /// <summary>Records the server refused. Retrying these unchanged will fail again.</summary>
    public IEnumerable<RecordOutcome> Rejected =>
        Outcomes.Where(o => o.Status == RecordWriteStatus.Rejected);

    /// <summary>True when every record in the batch was settled.</summary>
    public bool AllSettled => Outcomes.All(o => o.IsSettled);
}
