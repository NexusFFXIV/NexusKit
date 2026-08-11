using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace NexusKit.Sync.Contracts;

/// <summary>
/// A contract's <c>major.minor</c> version.
/// <para>Only two components, on purpose. The major is the compatibility boundary a client
/// and server must agree on; the minor expresses additive evolution within it. There is no
/// patch component because a contract has no implementation to fix — a change either alters
/// the shape of the data (minor or major) or it is not a change at all.</para>
/// </summary>
public readonly record struct ContractVersion(int Major, int Minor)
    : IComparable<ContractVersion>
{
    /// <summary>Parses <c>"1.0"</c>. Throws on anything else.</summary>
    /// <exception cref="FormatException">The text is not a valid <c>major.minor</c> pair.</exception>
    public static ContractVersion Parse(string text) =>
        TryParse(text, out var v)
            ? v
            : throw new FormatException(
                $"'{text}' is not a contract version. Expected 'major.minor', e.g. '1.0'.");

    /// <summary>Parses <c>"1.0"</c> without throwing.</summary>
    public static bool TryParse([NotNullWhen(true)] string? text, out ContractVersion version)
    {
        version = default;
        if (string.IsNullOrEmpty(text)) return false;

        var dot = text.IndexOf('.');
        if (dot <= 0 || dot == text.Length - 1) return false;

        // A second dot means someone passed a three-part version. Rejecting it is kinder
        // than silently reading "1.2.3" as 1.2 and negotiating against the wrong thing.
        if (text.IndexOf('.', dot + 1) >= 0) return false;

        var majorSpan = text.AsSpan(0, dot);
        var minorSpan = text.AsSpan(dot + 1);

        if (!int.TryParse(majorSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var major)) return false;
        if (!int.TryParse(minorSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var minor)) return false;

        version = new ContractVersion(major, minor);
        return true;
    }

    /// <summary>True when both versions share a major and can therefore negotiate.</summary>
    public bool IsCompatibleWith(ContractVersion other) => Major == other.Major;

    /// <inheritdoc />
    public int CompareTo(ContractVersion other)
    {
        var byMajor = Major.CompareTo(other.Major);
        return byMajor != 0 ? byMajor : Minor.CompareTo(other.Minor);
    }

    public static bool operator <(ContractVersion left, ContractVersion right) => left.CompareTo(right) < 0;
    public static bool operator >(ContractVersion left, ContractVersion right) => left.CompareTo(right) > 0;
    public static bool operator <=(ContractVersion left, ContractVersion right) => left.CompareTo(right) <= 0;
    public static bool operator >=(ContractVersion left, ContractVersion right) => left.CompareTo(right) >= 0;

    /// <inheritdoc />
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Major}.{Minor}");
}
