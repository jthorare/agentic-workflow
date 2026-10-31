using System.Globalization;
using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Concepts;

/// <summary>
/// The results a pin-scoped search read and left out, counted by the pin they sit in (concept pins spec §3.2). They are counts among the rows read, not totals,
/// and cost no second search; the sources count into it while they run side by side, so it is safe to share.
/// </summary>
public sealed class PinTally
{
    private const int MostPinsNamed = 5;

    private readonly Lock _gate = new();
    private readonly Dictionary<string, int> _byPin = new(StringComparer.Ordinal);
    private int _noPin;

    /// <summary>Counts one result left out.</summary>
    /// <param name="pin">The visible pin it sits in, or null for none.</param>
    public void Count(ConceptId? pin)
    {
        lock (_gate)
        {
            if (pin is { } held)
            {
                _byPin[held.Value] = _byPin.GetValueOrDefault(held.Value) + 1;
            }
            else
            {
                _noPin++;
            }
        }
    }

    /// <summary>The answer's note when nothing was inside the pin.</summary>
    /// <param name="scope">The pin the query is scoped to.</param>
    /// <param name="matchesNothing">True when the pin holds no file of this map at all.</param>
    /// <returns>One sentence: where the results read were, and the way out.</returns>
    public string EmptyNote(ConceptId scope, bool matchesNothing)
    {
        if (matchesNothing)
        {
            return $"concept {scope.Value} matches no file in this map (check the paths of pin '{scope.Value}' in index.concepts)";
        }

        List<KeyValuePair<string, int>> pins;
        int none;
        lock (_gate)
        {
            pins = [.. _byPin.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal).Take(MostPinsNamed)];
            none = _noPin;
        }

        if (pins.Count == 0 && none == 0)
        {
            return $"concept {scope.Value}: no results, and none outside it either";
        }

        var parts = pins.Select(p => string.Create(CultureInfo.InvariantCulture, $"{p.Value} were in {p.Key}")).ToList();
        if (none > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{none} were in no pin"));
        }

        var read = parts.Count == 1 ? parts[0] : string.Join(", ", parts[..^1]) + " and " + parts[^1];
        var fix = pins.Count > 0 ? $"concept={pins[0].Key}, or drop concept=" : "drop concept=";
        return $"concept {scope.Value}: no results. Of the results read, {read} (fix: {fix})";
    }
}
