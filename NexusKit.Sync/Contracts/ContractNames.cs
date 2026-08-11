namespace NexusKit.Sync.Contracts;

/// <summary>
/// Identifier rules for contract ids, collection names and field names.
/// <para>These names travel a long way: into JSON keys, into storage paths, into index
/// names, into OAuth-style scope strings like <c>reports:push</c>, and into URLs. Every one
/// of those has its own idea of what is legal, so the contract picks a conservative subset
/// that survives all of them rather than discovering the edge cases one integration at a
/// time.</para>
/// </summary>
public static class ContractNames
{
    /// <summary>Maximum length of a contract id.</summary>
    public const int MaxContractIdLength = 128;

    /// <summary>Maximum length of a collection or field name.</summary>
    public const int MaxNameLength = 64;

    /// <summary>
    /// Validates a contract id: dot-separated lowercase segments, e.g.
    /// <c>acme.venuetracker</c>. At least two segments are required — the leading segment is
    /// the author's namespace, and without it two unrelated authors both calling their
    /// contract <c>tracker</c> collide the moment their contracts meet on one server.
    /// </summary>
    public static bool IsValidContractId(string? id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > MaxContractIdLength) return false;

        var segments = 0;
        var segmentLength = 0;

        foreach (var c in id)
        {
            if (c == '.')
            {
                if (segmentLength == 0) return false;   // leading dot or ".."
                segments++;
                segmentLength = 0;
                continue;
            }

            if (!IsLowerAlphanumeric(c) && c != '-') return false;
            if (segmentLength == 0 && !IsLower(c)) return false;   // segment must start with a letter
            segmentLength++;
        }

        if (segmentLength == 0) return false;   // trailing dot
        segments++;

        return segments >= 2;
    }

    /// <summary>
    /// Validates a collection or field name: lowercase letters, digits and underscores,
    /// starting with a letter. No dots — a dot would be ambiguous against the scope
    /// separator and against JSON path notation in storage.
    /// </summary>
    public static bool IsValidName(string? name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > MaxNameLength) return false;
        if (!IsLower(name[0])) return false;

        foreach (var c in name)
        {
            if (!IsLowerAlphanumeric(c) && c != '_') return false;
        }

        return true;
    }

    private static bool IsLower(char c) => c is >= 'a' and <= 'z';

    private static bool IsLowerAlphanumeric(char c) => IsLower(c) || c is >= '0' and <= '9';
}
