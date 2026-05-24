namespace NexusKit.Core;

/// <summary>
/// Where the local player currently is. Passed into foreign-plugin adapters so
/// they can choose between "stay on this world" vs "cross-world" command forms
/// without depending on Dalamud's <c>IObjectTable</c> themselves.
/// <para><see cref="WorldId"/> is the <b>current</b> world (where the player is
/// right now, including while world-visiting), not the home world.</para>
/// </summary>
public sealed record PlayerLocation(uint? WorldId);
