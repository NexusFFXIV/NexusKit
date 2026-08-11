using System.Globalization;

namespace NexusKit.Sync.Contracts;

/// <summary>The outcome of comparing a candidate contract version against the registered one.</summary>
/// <param name="IsCompatible">True when the candidate may be registered as a new minor.</param>
/// <param name="BreakingChanges">Every incompatibility found, in detection order.</param>
public sealed record CompatibilityResult(bool IsCompatible, IReadOnlyList<string> BreakingChanges)
{
    /// <summary>A clean result.</summary>
    public static CompatibilityResult Compatible { get; } = new(true, []);

    /// <summary>All breaking changes on one line, for logs and error payloads.</summary>
    public override string ToString() =>
        IsCompatible ? "compatible" : string.Join("; ", BreakingChanges);
}

/// <summary>
/// Decides whether a new contract version can replace the registered one without breaking
/// peers that still speak the old one.
/// <para>The model is the one schema registries settled on: within a major version, evolution
/// must be <b>additive</b>. Anything that could make a previously valid record invalid, or a
/// previously readable record unreadable, needs a new major — because there is no moment at
/// which every deployed client updates at once, and a change that assumes there is will break
/// whoever updates last.</para>
/// </summary>
public static class ContractCompatibility
{
    /// <summary>
    /// Checks whether <paramref name="candidate"/> may be registered alongside
    /// <paramref name="registered"/>.
    /// </summary>
    /// <param name="registered">The version already known to the server.</param>
    /// <param name="candidate">The version being registered.</param>
    public static CompatibilityResult Check(SyncContract registered, SyncContract candidate)
    {
        ArgumentNullException.ThrowIfNull(registered);
        ArgumentNullException.ThrowIfNull(candidate);

        var breaking = new List<string>();

        if (!string.Equals(registered.ContractId, candidate.ContractId, StringComparison.Ordinal))
        {
            // Not an evolution at all — these are two different contracts.
            breaking.Add(
                $"Contract id changed from '{registered.ContractId}' to '{candidate.ContractId}'. "
                + "A different id is a different contract, not a new version of this one.");

            return new CompatibilityResult(false, breaking);
        }

        if (registered.Version.Major != candidate.Version.Major)
        {
            // A new major is *allowed* — it is how breaking changes ship — it just is not a
            // compatible evolution of this one, and both versions live side by side.
            breaking.Add(
                $"Major version changed from {registered.Version.Major} to {candidate.Version.Major}. "
                + "Register it as a separate major; it does not supersede the existing one.");

            return new CompatibilityResult(false, breaking);
        }

        if (candidate.Version <= registered.Version)
        {
            breaking.Add(
                $"Version {candidate.Version} does not advance on the registered {registered.Version}.");
        }

        foreach (var old in registered.Collections)
        {
            var updated = candidate.FindCollection(old.Name);
            if (updated is null)
            {
                breaking.Add($"Collection '{old.Name}' was removed; peers still using it would stop working.");
                continue;
            }

            CompareCollection(old, updated, breaking);
        }

        // New collections are additive by construction: nothing that exists today refers to
        // them, so nothing that exists today can break on them.

        return breaking.Count == 0 ? CompatibilityResult.Compatible : new CompatibilityResult(false, breaking);
    }

    private static void CompareCollection(CollectionDefinition old, CollectionDefinition updated, List<string> breaking)
    {
        var where = $"Collection '{old.Name}'";

        if (old.Direction != updated.Direction)
        {
            breaking.Add(
                $"{where} changed direction from {old.Direction} to {updated.Direction}. "
                + "Peers would be pushing to something that now only reads, or vice versa.");
        }

        if (!string.Equals(old.Key, updated.Key, StringComparison.Ordinal))
        {
            breaking.Add(
                $"{where} changed its key from '{old.Key}' to '{updated.Key}'. "
                + "Existing records are addressed by the old key and would become unreachable.");
        }

        foreach (var oldField in old.Fields)
        {
            var newField = updated.FindField(oldField.Name);
            if (newField is null)
            {
                breaking.Add($"{where} removed field '{oldField.Name}'.");
                continue;
            }

            CompareField(where, oldField, newField, breaking);
        }

        foreach (var newField in updated.Fields)
        {
            if (old.FindField(newField.Name) is not null) continue;

            if (newField.Required)
            {
                // An older client has never heard of this field and cannot send it, so every
                // write it makes would now fail validation.
                breaking.Add(
                    $"{where} added required field '{newField.Name}'. New fields must be optional; "
                    + "older peers cannot send a field they do not know about.");
            }
        }

        // Retention, rate limits and indexes deliberately do not appear here. They change how
        // the server treats data, not what a peer may send or read, so tightening them is an
        // operational decision rather than a compatibility break.
    }

    private static void CompareField(string where, FieldDefinition old, FieldDefinition updated, List<string> breaking)
    {
        var what = $"{where}, field '{old.Name}'";

        if (old.Type != updated.Type)
            breaking.Add($"{what} changed type from {old.Type} to {updated.Type}.");

        if (!old.Required && updated.Required)
        {
            breaking.Add(
                $"{what} became required. Peers built against the previous version may omit it, "
                + "so their writes would start failing.");
        }

        // Constraints may only loosen. A tightened bound turns records that were valid
        // yesterday into rejects today, which from a user's perspective is the feature
        // breaking rather than the contract evolving.
        if (updated.MaxLength is { } newMax && (old.MaxLength is null || newMax < old.MaxLength))
        {
            breaking.Add(
                $"{what} tightened MaxLength from {Describe(old.MaxLength)} to {newMax}.");
        }

        if (updated.Min is { } newMin && (old.Min is null || newMin > old.Min))
            breaking.Add($"{what} raised Min from {Describe(old.Min)} to {Number(newMin)}.");

        if (updated.Max is { } newMaxValue && (old.Max is null || newMaxValue < old.Max))
            breaking.Add($"{what} lowered Max from {Describe(old.Max)} to {Number(newMaxValue)}.");
    }

    private static string Describe(int? value) =>
        value is { } v ? v.ToString(CultureInfo.InvariantCulture) : "unbounded";

    private static string Describe(decimal? value) =>
        value is { } v ? Number(v) : "unbounded";

    private static string Number(decimal value) => value.ToString("G29", CultureInfo.InvariantCulture);
}
