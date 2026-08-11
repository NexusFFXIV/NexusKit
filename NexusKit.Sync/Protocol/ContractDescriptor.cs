using NexusKit.Sync.Contracts;

namespace NexusKit.Sync.Protocol;

/// <summary>
/// What a server publishes about a contract it has registered.
/// <para>Readable without authentication. That is deliberate: a contract document declares
/// shapes and constraints, not data, and being able to fetch one before holding a key is what
/// lets an author check compatibility, generate code, or simply see what a server speaks.</para>
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
