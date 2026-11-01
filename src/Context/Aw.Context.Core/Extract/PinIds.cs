using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Extract;

/// <summary>The one function that makes the ids of the pins of a build (spec 2026-10-27 §10.1): the home step and the concept step both call it, so a leaf '~billing' and 'belongs-to concept:billing' agree.</summary>
internal static class PinIds
{
    private const char Unit = '\u001f';

    /// <summary>The pins with their ids.</summary>
    /// <param name="pins">Every pin of the build, in order; a pin repeated once per repo is one pin.</param>
    /// <returns>One entry per distinct pin, in order; the ids are unique and never 'other'.</returns>
    internal static IReadOnlyList<ResolvedPin> Resolve(IEnumerable<ConceptPin> pins)
    {
        ArgumentNullException.ThrowIfNull(pins);
        var taken = new HashSet<string>(StringComparer.Ordinal) { ConceptKeys.Other["concept:".Length..] };
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var resolved = new List<ResolvedPin>();
        foreach (var pin in pins.Where(p => seen.Add(Identity(p))))
        {
            resolved.Add(new ResolvedPin(ConceptSlug.Unique(pin.Id?.Value ?? pin.Name, taken), pin));
        }

        return resolved;
    }

    /// <summary>The resolved entries of one repo's own pins, in the repo's order.</summary>
    /// <param name="resolved">The resolved pins of the whole build.</param>
    /// <param name="own">The repo's pins.</param>
    /// <returns>The repo's pins with their build-wide ids.</returns>
    internal static IReadOnlyList<ResolvedPin> Of(IReadOnlyList<ResolvedPin> resolved, IEnumerable<ConceptPin> own)
    {
        var wanted = own.Select(Identity).ToList();
        return [.. wanted.Distinct(StringComparer.Ordinal).Select(w => resolved.FirstOrDefault(p => Identity(p.Pin) == w)).OfType<ResolvedPin>()];
    }

    internal static string Identity(ConceptPin pin) =>
        string.Join(Unit, pin.Id?.Value, pin.Name, pin.Summary, string.Join(',', pin.Features), string.Join(',', pin.Paths), string.Join(',', pin.DependsOn), string.Join(',', pin.Owns), string.Join(',', pin.Aliases));
}
