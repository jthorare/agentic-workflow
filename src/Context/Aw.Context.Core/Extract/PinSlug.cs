using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Extract;

/// <summary>The id a pin without an 'id' gets from its name (concept pins spec §3.6): the slug the build derives, so a write names the same pin the map shows.</summary>
public static class PinSlug
{
    /// <summary>The ids pins get in a build: the one rule (<c xml:lang="en">PinIds</c>) the map, its per-pin hashes and the pins read share, so two pins that slug alike are 'foo' and 'foo-2' everywhere.</summary>
    /// <param name="build">Every pin of the build, in its order (the mapped repos' pins, each repo's after the profile's).</param>
    /// <param name="wanted">The pins to name; one the build lacks is named as if it came after the build's.</param>
    /// <returns>One id per wanted pin, in order.</returns>
    public static IReadOnlyList<string> IdsIn(IReadOnlyList<ConceptPin> build, IReadOnlyList<ConceptPin> wanted)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(wanted);
        var resolved = PinIds.Resolve([.. build, .. wanted]);
        return [.. wanted.Select(w => resolved.First(r => string.Equals(PinIds.Identity(r.Pin), PinIds.Identity(w), StringComparison.Ordinal)).Id)];
    }

    /// <summary>The slug of <paramref name="name"/>: lower-case ASCII words joined by '-' or '.', at most 64 characters.</summary>
    /// <param name="name">The pin's name.</param>
    /// <returns>The id the build gives a pin of that name when no other pin has taken it.</returns>
    public static string Of(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return ConceptSlug.Unique(name, []);
    }
}
