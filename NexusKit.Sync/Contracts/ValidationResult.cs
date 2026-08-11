namespace NexusKit.Sync.Contracts;

/// <summary>One reason a payload was rejected.</summary>
/// <param name="Field">
/// The offending field name, or null when the problem is with the record as a whole.
/// </param>
/// <param name="Message">Human-readable explanation, safe to return to the caller.</param>
public sealed record ValidationProblem(string? Field, string Message);

/// <summary>
/// The outcome of validating one record against a <see cref="CollectionDefinition"/>.
/// <para>Invalid data is an ordinary runtime condition — a client can be out of date, a user
/// can type nonsense into a form — so this returns a result rather than throwing. Only a
/// malformed <i>contract</i> throws, because that is a bug in the definition.</para>
/// </summary>
public sealed class ValidationResult
{
    private static readonly ValidationResult ValidInstance = new([]);

    private ValidationResult(IReadOnlyList<ValidationProblem> problems) => Problems = problems;

    /// <summary>A result with no problems.</summary>
    public static ValidationResult Valid => ValidInstance;

    /// <summary>Every problem found. Empty when the record is acceptable.</summary>
    public IReadOnlyList<ValidationProblem> Problems { get; }

    /// <summary>True when the record may be stored.</summary>
    public bool IsValid => Problems.Count == 0;

    /// <summary>Wraps a problem list, collapsing the empty case onto the shared instance.</summary>
    public static ValidationResult From(IReadOnlyList<ValidationProblem> problems) =>
        problems.Count == 0 ? ValidInstance : new ValidationResult(problems);

    /// <summary>All problems on one line, for logs and error payloads.</summary>
    public override string ToString() =>
        IsValid
            ? "valid"
            : string.Join("; ", Problems.Select(p => p.Field is null ? p.Message : $"{p.Field}: {p.Message}"));
}
