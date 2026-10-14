using Aw.Kernel;

namespace Aw.Sindri.Core.Events;

/// <summary>
/// An event-type filter (API spec §5.2 'types=', ReadLedger 'type='): a comma list of up to 16 globs, each a dotted
/// type in lower case ('[a-z0-9-]+(\.[a-z0-9-]+)*') with an optional final '.*'.
/// </summary>
public sealed record TypeFilter
{
    internal TypeFilter(IReadOnlyList<string> globs)
    {
        Globs = globs;
    }

    /// <summary>The globs, in the order given.</summary>
    public IReadOnlyList<string> Globs { get; }

    /// <summary>Parses <paramref name="text"/>; a malformed glob, an empty list or more than 16 globs is SND-CLI-003.</summary>
    /// <param name="text">The comma list.</param>
    /// <returns>The filter.</returns>
    public static Result<TypeFilter> Parse(string text)
        => throw new NotSupportedException("TypeFilter.Parse: P5 unit A makes the Typefilter_* tests pass");

    /// <summary>True when <paramref name="type"/> matches any glob.</summary>
    /// <param name="type">The event type.</param>
    /// <returns>Whether it matches.</returns>
    public bool Matches(string type)
        => throw new NotSupportedException("TypeFilter.Matches: P5 unit A makes the Typefilter_* tests pass");
}
