namespace Aw.Sindri.Core.Leftovers;

/// <summary>What the purge will delete.</summary>
/// <param name="Containers">Labelled containers.</param>
/// <param name="Volumes">Labelled volumes.</param>
/// <param name="Directory">The onyx/ directory when it exists.</param>
/// <param name="Keychain">The keychain entry Sindri recorded as `service/account`, or null.</param>
public sealed record PurgePlan(IReadOnlyList<string> Containers, IReadOnlyList<string> Volumes, string? Directory, string? Keychain)
{
    /// <summary>Gets a value indicating whether there is nothing to delete.</summary>
    public bool IsEmpty => Containers.Count == 0 && Volumes.Count == 0 && Directory is null && Keychain is null;
}
