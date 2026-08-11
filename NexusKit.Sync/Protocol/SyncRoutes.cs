using System.Globalization;
using NexusKit.Sync.Contracts;

namespace NexusKit.Sync.Protocol;

/// <summary>
/// Builds every URL path in the protocol.
/// <para>Lives in the shared assembly so client and server derive routes from the same code
/// rather than from two string literals that agree until one of them is edited. A typo here
/// breaks a test; a typo in a hand-written route on one side breaks production for whichever
/// peer updates second.</para>
/// </summary>
public static class SyncRoutes
{
    /// <summary>Version-prefixed root, e.g. <c>v1</c>.</summary>
    public static string Root => SyncProtocolVersion.PathSegment;

    /// <summary>Handshake endpoint.</summary>
    public static string Handshake() => $"{Root}/handshake";

    /// <summary>Contract listing — every contract the server has registered.</summary>
    public static string Contracts() => $"{Root}/contracts";

    /// <summary>A single contract document, optionally pinned to a version.</summary>
    public static string Contract(string contractId, ContractVersion? version = null)
    {
        var path = $"{Root}/contracts/{Escape(contractId)}";
        return version is { } v ? $"{path}?version={Escape(v.ToString())}" : path;
    }

    /// <summary>Push endpoint for one uplink collection. The version travels in the body.</summary>
    public static string Push(string contractId, string collection) =>
        $"{Root}/{Escape(contractId)}/{Escape(collection)}/push";

    /// <summary>
    /// Pull endpoint for one downlink collection.
    /// <para>The version is a query parameter here rather than a body field, because a pull is
    /// a GET — and keeping it in the URL means a cache or a log line records which contract
    /// version a response actually belongs to.</para>
    /// </summary>
    public static string Pull(
        string contractId,
        ContractVersion version,
        string collection,
        long since,
        int? limit = null)
    {
        var path = $"{Root}/{Escape(contractId)}/{Escape(collection)}/pull"
                   + $"?version={Escape(version.ToString())}"
                   + $"&since={since.ToString(CultureInfo.InvariantCulture)}";

        return limit is { } l ? $"{path}&limit={l.ToString(CultureInfo.InvariantCulture)}" : path;
    }

    /// <summary>Liveness probe.</summary>
    public static string Health() => "health";

    /// <summary>Readiness probe.</summary>
    public static string Ready() => "ready";

    // Contract ids and collection names are already restricted to characters that are safe in
    // a path (see ContractNames), so escaping is belt-and-braces rather than load-bearing —
    // but it costs nothing and stops a future relaxation of those rules from becoming an
    // injection bug.
    private static string Escape(string value) => Uri.EscapeDataString(value);
}
