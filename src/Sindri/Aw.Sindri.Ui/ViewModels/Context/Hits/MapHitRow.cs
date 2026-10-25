using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.ViewModels.Context.Hits;

/// <summary>One row of the hits list: a hit of the map search, in words. The texts are plain text; a snippet that came fenced is never parsed.</summary>
internal sealed class MapHitRow
{
    /// <summary>Makes the row for a hit.</summary>
    /// <param name="hit">The hit.</param>
    internal MapHitRow(MapSearchHit hit)
    {
        ArgumentNullException.ThrowIfNull(hit);
        Hit = hit;
    }

    /// <summary>The hit the row is for.</summary>
    internal MapSearchHit Hit { get; }

    /// <summary>The name the row has on screen and for the UI driver: the label.</summary>
    public string Name => Hit.Label;

    /// <summary>The hit's kind as served.</summary>
    public string Kind => Hit.Kind;

    /// <summary>"matched in: title"; empty when the hit does not say.</summary>
    public string MatchedText => Hit.Matched is { Length: > 0 } where ? Labels.ContextHitMatchedIn(where) : string.Empty;

    /// <summary>Whether <see cref="MatchedText"/> has words to show.</summary>
    public bool HasMatched => MatchedText.Length > 0;

    /// <summary>The matching text; empty when none.</summary>
    public string Snippet => Hit.Snippet ?? string.Empty;

    /// <summary>Whether <see cref="Snippet"/> has words to show.</summary>
    public bool HasSnippet => Snippet.Length > 0;
}
