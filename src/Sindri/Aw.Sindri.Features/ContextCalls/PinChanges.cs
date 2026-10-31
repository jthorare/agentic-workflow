namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// How the approved profile's pins differ from the ones the current map was built with (concept pins spec §3.7), by the hash of each pin's rule line (<c xml:lang="en">MapRuleset.PinHashes</c>). A map with no
/// per-pin record (built before migration 0028) or no map at all makes every answer unknown (null): never 'created'.
/// </summary>
/// <param name="Approved">The hash of each approved pin by id.</param>
/// <param name="Built">The hash of each pin the current map was built with by id, or null when it recorded none.</param>
internal sealed class PinChanges(IReadOnlyDictionary<string, string>? Approved, IReadOnlyDictionary<string, string>? Built)
{
    internal const string Created = "created";
    internal const string Updated = "updated";
    internal const string Removed = "removed";

    /// <summary>'created', 'updated', or null (the map has it, the approved profile lacks it, or nothing is known) for the approved pin with this id.</summary>
    internal string? Of(string id) =>
        Approved is null || Built is null || !Approved.TryGetValue(id, out var approved) ? null
        : !Built.TryGetValue(id, out var built) ? Created
        : string.Equals(approved, built, StringComparison.Ordinal) ? null : Updated;

    /// <summary>The ids the map was built with that the approved profile no longer has, in id order.</summary>
    internal IEnumerable<string> RemovedIds() =>
        Approved is null || Built is null ? [] : Built.Keys.Where(id => !Approved.ContainsKey(id)).Order(StringComparer.Ordinal);
}
