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
/// <param name="SupportedVersion">
/// The highest version this client could speak, when it knows — which differs from
/// <paramref name="Version"/> whenever it is deliberately staying behind. <b>Purely informational</b>,
/// and the only way an operator can tell "nobody has moved up yet" from "nobody can": both look
/// identical in the negotiated version alone, and only one of them means an old version is safe to
/// retire.
/// <para>Optional and additive, so the protocol version stays at 1. Clients built before this field
/// existed simply omit it, and null is recorded as "did not say" rather than guessed at.</para>
/// </param>
public sealed record HandshakeRequest(
    string ContractId,
    ContractVersion Version,
    string ContractHash,
    string ClientAgent,
    int ProtocolVersion,
    ContractVersion? SupportedVersion = null)
{
    /// <summary>Builds a handshake for a contract the client holds, using this build's protocol version.</summary>
    /// <param name="contract">The document the client is working from.</param>
    /// <param name="clientAgent">Free-form identification for the audit log.</param>
    /// <param name="supportedVersion">
    /// The highest version this client could speak, if it has looked. Pass the value from
    /// <c>ContractResolution</c> so an operator can see who is behind by choice.
    /// </param>
    public static HandshakeRequest For(
        SyncContract contract,
        string clientAgent,
        ContractVersion? supportedVersion = null)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientAgent);

        return new HandshakeRequest(
            contract.ContractId,
            contract.Version,
            contract.Hash,
            clientAgent,
            SyncProtocolVersion.Current,
            supportedVersion);
    }
}
