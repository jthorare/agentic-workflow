using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Concepts;

/// <summary>
/// The results a pin-scoped search read and left out, counted by the pin they sit in (concept pins spec §3.2). They are counts among the rows read, not totals,
/// and cost no second search; the sources count into it while they run side by side, so it is safe to share.
/// </summary>
public sealed class PinTally
{
    /// <summary>Counts one result left out.</summary>
    /// <param name="pin">The visible pin it sits in, or null for none.</param>
    public void Count(ConceptId? pin) => throw new NotSupportedException();

    /// <summary>The answer's note when nothing was inside the pin.</summary>
    /// <param name="scope">The pin the query is scoped to.</param>
    /// <param name="matchesNothing">True when the pin holds no file of this map at all.</param>
    /// <returns>One sentence: where the results read were, and the way out.</returns>
    public string EmptyNote(ConceptId scope, bool matchesNothing) => throw new NotSupportedException();
}
