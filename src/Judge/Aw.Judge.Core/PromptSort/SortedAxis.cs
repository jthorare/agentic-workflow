namespace Aw.Judge.Core.PromptSort;

/// <summary>One axis of one sorted prompt (a 'prompt_sort_axes' row): the blended value and where it came from.</summary>
/// <param name="Axis">The axis name, e.g. 'complexity' or 'touchesUi'.</param>
/// <param name="Value">The blended value, as text.</param>
/// <param name="Heuristic">What the keyword heuristic said, as text.</param>
/// <param name="Source">Where the value came from: 'heuristic', 'judge' or 'blend'.</param>
/// <param name="Status">How the judge and the heuristic related: 'agreed', 'overrode', 'undecided' and the like.</param>
/// <param name="Probability">The judge's probability for the value, when it gave one.</param>
/// <param name="Confidence">The judge's confidence, when it gave one.</param>
public sealed record SortedAxis(string Axis, string Value, string Heuristic, string Source, string Status, double? Probability, double? Confidence);
