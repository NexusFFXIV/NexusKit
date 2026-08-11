namespace NexusKit.Sync.Protocol;

/// <summary>
/// The shape of a sync API key, shared by the side that issues one and the side that
/// carries one.
/// <para>The <c>nxs_</c> prefix is not decoration. It makes a leaked key recognisable in a log
/// file, a config export or a pasted screenshot, and it is the hook secret scanners match on —
/// which is how a key that escapes gets noticed by something other than the attacker.</para>
/// </summary>
public static class ApiKeyFormat
{
    /// <summary>Prefix every key carries.</summary>
    public const string Prefix = "nxs_";

    /// <summary>Number of random characters after the prefix.</summary>
    public const int BodyLength = 32;

    /// <summary>Total key length.</summary>
    public const int TotalLength = 4 + BodyLength;

    /// <summary>
    /// Crockford-style base32 alphabet, minus the characters people confuse when reading a key
    /// aloud or retyping it from a screenshot.
    /// </summary>
    public const string Alphabet = "0123456789abcdefghjkmnpqrstvwxyz";

    /// <summary>
    /// Checks the shape only. A well-formed key is not a valid key — only the server can say
    /// that — but rejecting a malformed one client-side turns a puzzling 401 into "you pasted
    /// this wrong".
    /// </summary>
    public static bool IsWellFormed(string? key)
    {
        if (key is null || key.Length != TotalLength) return false;
        if (!key.StartsWith(Prefix, StringComparison.Ordinal)) return false;

        for (var i = Prefix.Length; i < key.Length; i++)
        {
            if (Alphabet.IndexOf(key[i], StringComparison.Ordinal) < 0) return false;
        }

        return true;
    }

    /// <summary>
    /// Renders a key for display or logging as <c>nxs_abcd…wxyz</c>.
    /// <para>Use this anywhere a key might be written down. A key is a bearer credential:
    /// whoever reads the log has it.</para>
    /// </summary>
    public static string Redact(string? key)
    {
        if (string.IsNullOrEmpty(key)) return "(none)";
        if (key.Length < Prefix.Length + 8) return "(malformed)";

        return $"{key[..(Prefix.Length + 4)]}…{key[^4..]}";
    }
}
