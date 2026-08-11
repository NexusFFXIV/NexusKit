using NexusKit.Sync.Contracts;

namespace NexusKit.Sync.Protocol;

/// <summary>
/// The result for one record in a push batch.
/// <para>Outcomes are per record rather than per batch on purpose. One malformed record in a
/// batch of fifty should not force the other forty-nine to be resent, and a client that cannot
/// tell which record failed has no option but to retry the whole batch forever.</para>
/// </summary>
/// <param name="OpId">The operation id from the corresponding <see cref="RecordWrite"/>.</param>
/// <param name="Status">What happened.</param>
/// <param name="Problems">
/// Why it was rejected. Empty unless <paramref name="Status"/> is
/// <see cref="RecordWriteStatus.Rejected"/>.
/// </param>
public sealed record RecordOutcome(
    string OpId,
    RecordWriteStatus Status,
    IReadOnlyList<ValidationProblem>? Problems = null)
{
    /// <summary>True when the client may drop its outbox entry.</summary>
    public bool IsSettled => Status is RecordWriteStatus.Accepted or RecordWriteStatus.Duplicate;

    /// <summary>Creates an accepted outcome.</summary>
    public static RecordOutcome Accepted(string opId) => new(opId, RecordWriteStatus.Accepted);

    /// <summary>Creates a duplicate outcome.</summary>
    public static RecordOutcome Duplicate(string opId) => new(opId, RecordWriteStatus.Duplicate);

    /// <summary>Creates a rejected outcome carrying the validation problems.</summary>
    public static RecordOutcome Rejected(string opId, IReadOnlyList<ValidationProblem> problems) =>
        new(opId, RecordWriteStatus.Rejected, problems);
}
