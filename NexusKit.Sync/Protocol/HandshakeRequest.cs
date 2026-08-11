using NexusKit.Sync.Contracts;

namespace NexusKit.Sync.Protocol;

/// <summary>
/// Opens a session: the client states which contract it speaks and how it speaks it.
/// </summary>
/// <param name="ContractId">Stable identity, e.g. <c>acme.venuetracker</c>.</param>
/// <param name="Version">The <c>major.minor</c> version the client was built against.</param>
/// <param name="ContractHash">
/// The client's canonical contract hash. Carried for <b>diagnosis, not as a gate</b> — matching
/// on it would break every client on any trivial edit, whereas seeing both hashes in one log
/// line is what turns "contract mismatch" from a mystery into a diff.
/// </param>
/// <param name="ClientAgent">
/// Free-form identification, e.g. <c>MyPlugin/0.3.0</c>. Ends up in the audit log,
/// which is what makes "one build is hammering the API" a question with an answer.
/// </param>
/// <param name="ProtocolVersion">The wire version the client speaks. See <see cref="SyncProtocolVersion"/>.</param>
public sealed record HandshakeRequest(
    string ContractId,
    ContractVersion Version,
    string ContractHash,
    string ClientAgent,
    int ProtocolVersion)
{
    /// <summary>Builds a handshake for a contract the client holds, using this build's protocol version.</summary>
    public static HandshakeRequest For(SyncContract contract, string clientAgent)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientAgent);

        return new HandshakeRequest(
            contract.ContractId,
            contract.Version,
            contract.Hash,
            clientAgent,
            SyncProtocolVersion.Current);
    }
}
