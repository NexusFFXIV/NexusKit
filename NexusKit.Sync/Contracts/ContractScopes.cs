using System.Diagnostics.CodeAnalysis;

namespace NexusKit.Sync.Contracts;

/// <summary>
/// Derives the permission scopes a contract implies.
/// <para>Scopes are not declared separately from the contract — they are computed from it,
/// one per collection. That is deliberate: a hand-maintained scope list drifts from the
/// collections it is meant to guard, and the failure mode of that drift is a scope that
/// grants access to something nobody remembers approving.</para>
/// <para>The verb follows the collection's direction, so a scope can never express an
/// operation the collection does not support. There is no <c>venues:push</c> for a downlink
/// collection — not because the server rejects it, but because it cannot be named.</para>
/// </summary>
public static class ContractScopes
{
    /// <summary>Verb for writing to an uplink collection.</summary>
    public const string PushVerb = "push";

    /// <summary>Verb for reading a downlink collection.</summary>
    public const string PullVerb = "pull";

    /// <summary>Separator between collection name and verb.</summary>
    public const char Separator = ':';

    /// <summary>The verb implied by a direction.</summary>
    public static string VerbFor(SyncDirection direction) =>
        direction == SyncDirection.Uplink ? PushVerb : PullVerb;

    /// <summary>The single scope a collection implies, e.g. <c>reports:push</c>.</summary>
    public static string For(CollectionDefinition collection) =>
        $"{collection.Name}{Separator}{VerbFor(collection.Direction)}";

    /// <summary>Every scope the contract implies, ordered by collection name.</summary>
    public static IReadOnlyList<string> All(SyncContract contract)
    {
        var scopes = new List<string>(contract.Collections.Count);
        foreach (var collection in contract.Collections) scopes.Add(For(collection));

        // Collections already arrive name-sorted from SyncContract.Create, so the scope list
        // is stable without re-sorting — worth keeping true, since granted-scope lists end up
        // compared and stored.
        return scopes;
    }

    /// <summary>Splits <c>reports:push</c> into its parts. Returns false on anything malformed.</summary>
    public static bool TryParse(
        [NotNullWhen(true)] string? scope,
        [NotNullWhen(true)] out string? collection,
        [NotNullWhen(true)] out string? verb)
    {
        collection = null;
        verb = null;
        if (string.IsNullOrEmpty(scope)) return false;

        var separator = scope.IndexOf(Separator);
        if (separator <= 0 || separator == scope.Length - 1) return false;
        if (scope.IndexOf(Separator, separator + 1) >= 0) return false;

        var name = scope[..separator];
        var v = scope[(separator + 1)..];

        if (!ContractNames.IsValidName(name)) return false;
        if (v is not (PushVerb or PullVerb)) return false;

        collection = name;
        verb = v;
        return true;
    }
}
