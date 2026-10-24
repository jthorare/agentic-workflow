namespace Aw.Judge.Core.PromptSort;

/// <summary>One scaffold: a note the sorter adds to a prompt when its driving axes say so.</summary>
/// <param name="Id">The scaffold id: brief, bugfix, ui-evidence or plan-first.</param>
/// <param name="NeedsJudge">The axes Jev itself must have decided (a heuristic-only axis never fires a scaffold).</param>
/// <param name="Applies">Whether the sorted values call for it.</param>
/// <param name="Text">The note.</param>
/// <param name="RequiresUiEvidence">Whether it also sets the done gate's UI-evidence requirement.</param>
internal sealed record Scaffold(string Id, IReadOnlyList<string> NeedsJudge, Func<SortValues, bool> Applies, string Text, bool RequiresUiEvidence = false);
