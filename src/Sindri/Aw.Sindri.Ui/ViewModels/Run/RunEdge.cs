namespace Aw.Sindri.Ui.ViewModels.Run;

/// <summary>One edge between cards.</summary>
/// <param name="From">The source card.</param>
/// <param name="To">The target card.</param>
/// <param name="Kind">Main, loop or end.</param>
/// <param name="Outcome">The outcome the edge leaves on, when it is known.</param>
/// <param name="Label">The pill text, or null when none is served (none is invented).</param>
/// <param name="Tone">How it is drawn.</param>
internal sealed record RunEdge(string From, string To, RunEdgeKind Kind, string? Outcome, string? Label, EdgeTone Tone)
{
    /// <summary>Gets a key unique among a graph's edges.</summary>
    internal string Key => From + ">" + To;
}
