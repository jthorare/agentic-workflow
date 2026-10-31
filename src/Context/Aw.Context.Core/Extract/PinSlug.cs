namespace Aw.Context.Core.Extract;

/// <summary>The id a pin without an 'id' gets from its name (concept pins spec §3.6): the slug the build derives, so a write names the same pin the map shows.</summary>
public static class PinSlug
{
    /// <summary>The slug of <paramref name="name"/>: lower-case ASCII words joined by '-' or '.', at most 64 characters.</summary>
    /// <param name="name">The pin's name.</param>
    /// <returns>The id the build gives a pin of that name when no other pin has taken it.</returns>
    public static string Of(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return ConceptSlug.Unique(name, []);
    }
}
