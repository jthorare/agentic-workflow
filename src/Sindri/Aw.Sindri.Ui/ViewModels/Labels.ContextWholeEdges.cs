namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The words of the whole map's edges and legend (q86).</summary>
internal static partial class Labels
{
    /// <summary>The pill of a node edge: its words, and for a suggestion how sure the tool is.</summary>
    /// <param name="type">The served edge type.</param>
    /// <param name="confirmed">True when a person confirmed the edge.</param>
    /// <param name="confidence">How sure the tool is, 0 to 1.</param>
    /// <returns>"part of", or "part of · suggested 62%".</returns>
    internal static string ContextWholeEdgePill(string type, bool confirmed, double confidence) =>
        confirmed ? ContextMapEdgeWord(type) : ContextMapEdgeWord(type) + " · " + ContextMapSuggestedPct((int)Math.Round(confidence * 100, MidpointRounding.AwayFromZero));
}
