using NexusKit.Sync.Contracts;

namespace NexusKit.Sync.Protocol;

/// <summary>
/// Identifies a contract without carrying the whole document.
/// </summary>
/// <param name="ContractId">Stable identity, e.g. <c>acme.venuetracker</c>.</param>
/// <param name="Version">The <c>major.minor</c> version the peer is asking about.</param>
public sealed record ContractRef(string ContractId, ContractVersion Version)
{
    /// <summary>Builds a reference from a contract the caller already holds.</summary>
    public static ContractRef Of(SyncContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        return new ContractRef(contract.ContractId, contract.Version);
    }

    /// <inheritdoc />
    public override string ToString() => $"{ContractId}@{Version}";
}
