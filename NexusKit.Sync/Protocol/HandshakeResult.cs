using NexusKit.Sync.Contracts;

namespace NexusKit.Sync.Protocol;

/// <summary>
/// What the server agreed to.
/// </summary>
/// <param name="NegotiatedVersion">
/// The contract version actually in force: the highest minor both sides support within the
/// shared major. A client asking for 1.4 against a server on 1.2 negotiates 1.2 and must not
/// use anything newer.
/// </param>
/// <param name="ServerContractHash">The server's canonical hash, for diagnosis.</param>
/// <param name="GrantedScopes">
/// The scopes this API key actually carries, which may be a subset of what the contract
/// implies. A client should treat a missing scope as a disabled feature rather than an error
/// to retry — the user simply did not grant it.
/// </param>
/// <param name="Limits">Batch and payload bounds to size requests against.</param>
/// <param name="SessionToken">
/// Optional short-lived token the client may present instead of the API key on subsequent
/// calls. Purely an optimisation; a client that ignores it and keeps sending the key stays
/// correct.
/// </param>
/// <param name="ServerMessage">
/// Optional operator notice — planned maintenance, a deprecation warning, "this server moves
/// next week". Surfacing it beats leaving users to discover an outage on their own.
/// </param>
public sealed record HandshakeResult(
    ContractVersion NegotiatedVersion,
    string ServerContractHash,
    IReadOnlyList<string> GrantedScopes,
    SyncLimits Limits,
    string? SessionToken = null,
    string? ServerMessage = null)
{
    /// <summary>True when the granted scopes permit the given collection operation.</summary>
    public bool HasScope(string scope) => GrantedScopes.Contains(scope, StringComparer.Ordinal);

    /// <summary>True when the granted scopes permit this collection's one operation.</summary>
    public bool HasScopeFor(CollectionDefinition collection)
    {
        ArgumentNullException.ThrowIfNull(collection);
        return HasScope(collection.Scope);
    }
}
