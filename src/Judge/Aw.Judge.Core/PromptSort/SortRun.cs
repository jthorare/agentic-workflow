namespace Aw.Judge.Core.PromptSort;

/// <summary>How one prompt's sort ran (a 'prompt_sort_runs' row): the mode and which scaffolds would have fired, fired or were held back.</summary>
/// <param name="Mode">'blend' (the judge and the heuristics) or 'heuristic-only'.</param>
/// <param name="Reason">Why, e.g. 'sorted', 'no-jev' or 'timeout'.</param>
/// <param name="WouldFire">The scaffolds that would fire were their switches on.</param>
/// <param name="Fired">The scaffolds that fired (their switches are on).</param>
/// <param name="Suppressed">The scaffolds held back, each with its reason, as JSON objects.</param>
public sealed record SortRun(string Mode, string Reason, IReadOnlyList<string> WouldFire, IReadOnlyList<string> Fired, IReadOnlyList<string> Suppressed);
