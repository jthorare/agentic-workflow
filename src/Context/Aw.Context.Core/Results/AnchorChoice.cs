namespace Aw.Context.Core.Results;

/// <summary>One node offered to a caller in place of a name that matched nothing or several (#2809): the key to ask by, and the label and kind a person reads.</summary>
/// <param name="Key">The node's key, the anchor a pick reads.</param>
/// <param name="Label">The node's label, as the map's cards show it.</param>
/// <param name="Kind">The node's kind ('feature', 'action', 'concept', ...).</param>
public sealed record AnchorChoice(string Key, string Label, string Kind);
