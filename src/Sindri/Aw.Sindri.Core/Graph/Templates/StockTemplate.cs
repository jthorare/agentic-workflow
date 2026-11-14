namespace Aw.Sindri.Core.Graph.Templates;

/// <summary>One template of the Workflows page start screen (spec E7, E13).</summary>
/// <param name="Key">'A', 'B' or 'C'.</param>
/// <param name="Title">What the card shows.</param>
/// <param name="Trigger">The trigger's event kind, or 'schedule' or 'manual'.</param>
/// <param name="Needs">What it needs before it can run, such as 'connection:github'.</param>
/// <param name="Spec">The graph; fields the person must fill (the repo) are left empty.</param>
public sealed record StockTemplate(string Key, string Title, string Trigger, IReadOnlyList<string> Needs, GraphSpec Spec);
