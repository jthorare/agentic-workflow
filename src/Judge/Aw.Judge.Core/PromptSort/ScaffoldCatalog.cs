namespace Aw.Judge.Core.PromptSort;

/// <summary>
/// The four scaffolds. No generic skills: Prism's routing hook already matches keywords to skills. The one workflow named
/// here, /bugFixOrchestrator, is not in its matcher.
/// </summary>
internal static class ScaffoldCatalog
{
    internal static IReadOnlyList<Scaffold> All { get; } =
    [
        // Every axis the promotion rule measures for brief: acting on a judged ambiguity but a heuristic verification read
        // would fire on a signal the eval never validated.
        new Scaffold(
            "brief",
            [SortAxes.Ambiguity, SortAxes.VerificationDefined, SortAxes.IsTask],
            static v => v.Flag(SortAxes.IsTask) && v.Is(SortAxes.Ambiguity, "vague") && !v.Flag(SortAxes.VerificationDefined) && !v.Is(SortAxes.Complexity, "trivial"),
            "Before editing, state in two lines the Goal and how you will Verify it (a test, a command's output, or a screenshot). If you cannot say how to verify it, ask one question first."),
        new Scaffold(
            "bugfix",
            [SortAxes.IsBugReport],
            static v => v.Flag(SortAxes.IsBugReport),
            "This reads as a bug report. /bugFixOrchestrator drives a bug to a fix proven by the check that failed before it; otherwise reproduce the failure before you change code."),
        new Scaffold(
            "ui-evidence",
            [SortAxes.TouchesUi],
            static v => v.Flag(SortAxes.IsTask) && v.Flag(SortAxes.TouchesUi),
            "This changes what users see. Before you say it is done, show UI evidence (a screenshot, a Playwright run or an iOS snapshot); the done gate will ask for it.",
            RequiresUiEvidence: true),
        new Scaffold(
            "plan-first",
            [SortAxes.Complexity],
            static v => v.Flag(SortAxes.IsTask) && v.Is(SortAxes.Complexity, "large"),
            "This looks large. Outline a short plan first (files to touch, order, how each step is verified) and confirm it before a long run."),
    ];
}
