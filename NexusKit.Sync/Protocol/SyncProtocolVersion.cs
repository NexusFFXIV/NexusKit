namespace NexusKit.Sync.Protocol;

/// <summary>
/// The version of the wire protocol itself — distinct from the NuGet package version and from
/// any individual contract's version.
/// <para>Three version numbers travel through this system and conflating them causes real
/// breakage:</para>
/// <list type="table">
///   <item>
///     <term>Package version</term>
///     <description>Derived from the git tag. Moves on any shipped change, including docs.</description>
///   </item>
///   <item>
///     <term>Protocol version (this)</term>
///     <description>Moves only when the wire surface changes incompatibly. Every client and
///     server has to agree on it, including implementations we did not write.</description>
///   </item>
///   <item>
///     <term>Contract version</term>
///     <description>Belongs to whoever authored the contract. Not ours to move.</description>
///   </item>
/// </list>
/// </summary>
public static class SyncProtocolVersion
{
    /// <summary>The protocol version this build speaks.</summary>
    public const int Current = 1;

    /// <summary>
    /// The oldest protocol version this build still accepts from a peer. Equal to
    /// <see cref="Current"/> today because there is only one version; when a second arrives,
    /// this is the knob that decides how long old peers keep working.
    /// </summary>
    public const int MinimumSupported = 1;

    /// <summary>URL path segment carrying the protocol version, e.g. <c>v1</c>.</summary>
    public static string PathSegment => $"v{Current}";

    /// <summary>True when a peer advertising <paramref name="version"/> can be served.</summary>
    public static bool IsSupported(int version) => version is >= MinimumSupported and <= Current;
}
