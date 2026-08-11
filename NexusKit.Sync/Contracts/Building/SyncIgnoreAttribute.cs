namespace NexusKit.Sync.Contracts.Building;

/// <summary>
/// Marks a property the contract builder should not turn into a field.
/// <para>Only affects <see cref="SyncContractBuilder"/>, which derives fields by reflecting
/// over a POCO. It has no meaning in a hand-written contract document — the document is the
/// authority, and a field that is not in it simply does not exist.</para>
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false)]
public sealed class SyncIgnoreAttribute : Attribute;
