namespace NexusKit.Sync.Protocol;

/// <summary>
/// The one interface every client and server on this protocol programs against.
/// <para>Four operations, and no fifth. Keeping the surface this small is what makes the norm
/// enforceable: a conformance suite can cover all of it, an in-process implementation can
/// stand in for a whole server during tests, and a second transport — gRPC, a live socket —
/// can be added without a plugin author noticing.</para>
/// <para><b>Implementations must:</b></para>
/// <list type="bullet">
///   <item><description>throw <see cref="SyncProtocolException"/> for anything the peer
///     reported as a problem, and let genuine transport faults surface as themselves — the
///     caller decides what to retry, and it cannot decide well if the two look alike</description></item>
///   <item><description>honour the cancellation token on every call</description></item>
///   <item><description>be safe for concurrent use, since a client typically drains an outbox
///     and refreshes a mirror at the same time</description></item>
///   <item><description>treat <see cref="PushAsync"/> as idempotent per
///     <see cref="RecordWrite.OpId"/> — a retry after a lost response must not write twice</description></item>
/// </list>
/// </summary>
public interface ISyncProtocol
{
    /// <summary>
    /// Negotiates a contract version and reports the scopes the caller actually holds.
    /// </summary>
    /// <exception cref="SyncProtocolException">
    /// No compatible contract, an unusable protocol version, or a rejected key.
    /// </exception>
    Task<HandshakeResult> HandshakeAsync(HandshakeRequest request, CancellationToken ct);

    /// <summary>
    /// Submits a batch of writes to an uplink collection. Individual records may be rejected
    /// while the call itself succeeds — inspect <see cref="PushResult.Outcomes"/>.
    /// </summary>
    /// <exception cref="SyncProtocolException">
    /// The batch as a whole was refused: unknown collection, missing scope, wrong direction,
    /// over a limit.
    /// </exception>
    Task<PushResult> PushAsync(PushRequest request, CancellationToken ct);

    /// <summary>Reads one page of changes from a downlink collection.</summary>
    /// <exception cref="SyncProtocolException">
    /// Unknown collection, missing scope, or wrong direction.
    /// </exception>
    Task<PullResult> PullAsync(PullRequest request, CancellationToken ct);

    /// <summary>
    /// Fetches a registered contract document. Requires no authentication — a document
    /// describes shapes, not data.
    /// </summary>
    /// <exception cref="SyncProtocolException">Nothing is registered under that id and major.</exception>
    Task<ContractDescriptor> DescribeAsync(ContractRef reference, CancellationToken ct);
}
