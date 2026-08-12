using NexusKit.Sync.Contracts;

namespace NexusKit.Sync.Protocol;

/// <summary>
/// What a server publishes about a contract it has registered.
/// <para>Requires the built-in <c>contract:pull</c> scope. A contract document declares shapes and
/// constraints rather than data, but it also declares everything a server speaks, and a server
/// should not describe what it holds to anyone who asks. A key granted the scope is a statement that
/// this client may follow the server's schema — which is what lets it check compatibility, generate
/// code, or pick the newest version it can honour.</para>
/// </summary>
/// <param name="ContractId">The contract's identity.</param>
/// <param name="AvailableVersions">Every version registered here, ascending.</param>
/// <param name="Version">The version this descriptor's document represents.</param>
/// <param name="CanonicalJson">The canonical document, ready to hash or to parse.</param>
/// <param name="Hash">The server's hash of <paramref name="CanonicalJson"/>.</param>
public sealed record ContractDescriptor(
    string ContractId,
    IReadOnlyList<ContractVersion> AvailableVersions,
    ContractVersion Version,
    string CanonicalJson,
    string Hash)
{
    /// <summary>Parses the carried document back into a contract.</summary>
    /// <exception cref="ContractDefinitionException">The server sent an invalid document.</exception>
    public SyncContract ToContract() => ContractJson.Parse(CanonicalJson);

    /// <summary>
    /// The highest version registered here that shares a major with <paramref name="wanted"/>,
    /// or null when the server has nothing compatible. This is the negotiation rule from the
    /// client's side of the handshake.
    /// </summary>
    public ContractVersion? BestMatchFor(ContractVersion wanted)
    {
        ContractVersion? best = null;

        foreach (var candidate in AvailableVersions)
        {
            if (!candidate.IsCompatibleWith(wanted)) continue;
            if (best is null || candidate > best.Value) best = candidate;
        }

        return best;
    }
}
