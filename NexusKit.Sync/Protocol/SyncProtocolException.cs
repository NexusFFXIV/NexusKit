namespace NexusKit.Sync.Protocol;

/// <summary>
/// Thrown when the peer answered with a Problem Details response.
/// <para>Transport faults — DNS, TLS, a dropped connection — surface as their usual
/// exceptions instead. The distinction matters to a caller: a transport fault is worth
/// retrying, whereas most problems here will produce the identical answer next time and
/// retrying only burns the rate limit.</para>
/// </summary>
public sealed class SyncProtocolException : Exception
{
    /// <summary>Creates the exception from a problem payload.</summary>
    public SyncProtocolException(SyncProblem problem)
        : base(problem?.ToString() ?? throw new ArgumentNullException(nameof(problem))) =>
        Problem = problem;

    /// <summary>The problem the peer reported.</summary>
    public SyncProblem Problem { get; }

    /// <summary>
    /// True when retrying the identical request could plausibly succeed later: the server is
    /// unavailable, or the caller is over a limit that refills with time.
    /// </summary>
    public bool IsTransient =>
        Problem.Type == SyncProblemType.LimitExceeded || Problem.Status is 502 or 503 or 504;
}
