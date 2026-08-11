namespace NexusKit.Sync.Contracts;

/// <summary>
/// Thrown when a contract is structurally invalid — a malformed name, a key that names no
/// field, a constraint on a type that cannot carry it, and so on.
/// <para>This is about the contract <i>document</i>, not about data written against it. A
/// payload that violates a valid contract produces a validation result, not an exception:
/// bad data is an expected runtime condition, whereas a bad contract is a bug in the
/// author's definition and should fail loudly and early.</para>
/// </summary>
public sealed class ContractDefinitionException : Exception
{
    /// <summary>Creates the exception with a single message.</summary>
    public ContractDefinitionException(string message) : base(message) =>
        Problems = [message];

    /// <summary>
    /// Creates the exception from every problem found, so an author sees the whole list at
    /// once instead of fixing one, rebuilding, and discovering the next.
    /// </summary>
    public ContractDefinitionException(IReadOnlyList<string> problems)
        : base(problems.Count == 1
            ? problems[0]
            : $"The contract definition has {problems.Count} problems:{Environment.NewLine}  - "
              + string.Join($"{Environment.NewLine}  - ", problems)) =>
        Problems = problems;

    /// <summary>Every problem found, in the order they were detected.</summary>
    public IReadOnlyList<string> Problems { get; }
}
