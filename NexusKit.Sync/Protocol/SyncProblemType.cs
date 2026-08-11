namespace NexusKit.Sync.Protocol;

/// <summary>
/// The stable <c>type</c> URIs used in Problem Details responses (RFC 9457).
/// <para>These are identifiers, not links to fetch — a client branches on them. They are part
/// of the wire surface: renaming one is a breaking protocol change, because every peer that
/// handles a case by comparing against the old string silently stops handling it.</para>
/// </summary>
public static class SyncProblemType
{
    private const string Prefix = "https://nexusffxiv.dev/problems/";

    /// <summary>The peer's contract does not line up with the registered one.</summary>
    public const string ContractMismatch = Prefix + "contract-mismatch";

    /// <summary>No contract with that id and major version is registered here.</summary>
    public const string UnknownContract = Prefix + "unknown-contract";

    /// <summary>The contract exists, but not the named collection.</summary>
    public const string UnknownCollection = Prefix + "unknown-collection";

    /// <summary>The API key is missing, malformed, revoked or expired.</summary>
    public const string Unauthenticated = Prefix + "unauthenticated";

    /// <summary>The key is valid but lacks the scope this operation needs.</summary>
    public const string ScopeMissing = Prefix + "scope-missing";

    /// <summary>
    /// The operation contradicts the collection's direction — pushing to a downlink, or
    /// pulling a collection that only accepts writes.
    /// </summary>
    public const string DirectionViolation = Prefix + "direction-violation";

    /// <summary>One or more records failed contract validation.</summary>
    public const string ValidationFailed = Prefix + "validation-failed";

    /// <summary>The caller exceeded a rate limit or quota.</summary>
    public const string LimitExceeded = Prefix + "limit-exceeded";

    /// <summary>The peer speaks a protocol version this server cannot serve.</summary>
    public const string ProtocolUnsupported = Prefix + "protocol-unsupported";
}
