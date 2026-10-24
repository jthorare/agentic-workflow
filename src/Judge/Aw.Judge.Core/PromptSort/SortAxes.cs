using Aw.Judge.Core.Batching;

namespace Aw.Judge.Core.PromptSort;

/// <summary>The eleven axes the prompt sorter asks about every real prompt: nine yes/no (noul) and two scores, with the words Jev reads.</summary>
internal static class SortAxes
{
    internal const string IsTask = "is_task";
    internal const string WantsLoop = "wants_loop";
    internal const string ScopeDefined = "scope_defined";
    internal const string LimitsDefined = "limits_defined";
    internal const string ApproachDefined = "approach_defined";
    internal const string VerificationDefined = "verification_defined";
    internal const string IsBugReport = "is_bug_report";
    internal const string TouchesUi = "touches_ui";
    internal const string NeedsResearch = "needs_research";
    internal const string Complexity = "complexity";
    internal const string Ambiguity = "ambiguity";

    internal const string Yes = "true";
    internal const string No = "false";

    /// <summary>Decisive band: a yes/no counts only outside [Low, High]; a score counts only at confidence of at least MinConfidence.</summary>
    internal const double Low = 0.4;
    internal const double High = 0.6;
    internal const double MinConfidence = 0.4;

    internal static IReadOnlyList<string> Noul { get; } =
        [IsTask, WantsLoop, ScopeDefined, LimitsDefined, ApproachDefined, VerificationDefined, IsBugReport, TouchesUi, NeedsResearch];

    internal static IReadOnlyList<string> All { get; } = [.. Noul, Complexity, Ambiguity];

    internal static IReadOnlyList<string> ComplexityLevels { get; } = ["trivial", "small", "substantial", "large"];

    // "name: description", lowest first; Jev reads the descriptions.
    internal static IReadOnlyList<string> ComplexityCriteria { get; } =
    [
        "trivial: a one-line answer or a single small edit",
        "small: one file, under an hour of work",
        "substantial: several files, a new feature, or a refactor of one module",
        "large: a cross-cutting refactor, a new subsystem, or multi-day work",
    ];

    internal static IReadOnlyList<string> AmbiguityCriteria { get; } =
    [
        "clear: scope, target files or components, and done-criteria are explicit",
        "partly: the goal is clear but scope, limits, or done-criteria are missing",
        "vague: the goal itself is open to interpretation",
    ];

    // The 0..1 ambiguity value: 'partly' sits below the 0.5 line on its own; only real 'vague' mass crosses it.
    internal static IReadOnlyList<double> AmbiguityWeights { get; } = [0, 0.35, 1];

    internal static IReadOnlyList<BatchQuestion> Questions { get; } =
    [
        .. Noul.Select(static axis => (BatchQuestion)new NoulBatchQuestion(axis, Instruction(axis))),
        new ScoreBatchQuestion(Complexity, "How much work does fulfilling this request take?", ComplexityCriteria),
        new ScoreBatchQuestion(Ambiguity, "How underspecified is the request, judged by whether scope, limits, and acceptance criteria are stated?", AmbiguityCriteria),
    ];

    /// <summary>The collapsed class a scaffold's rule compares: complexity matters only as 'large', ambiguity only as 'vague'.</summary>
    internal static string Class(string axis, string value) => axis switch
    {
        Complexity => string.Equals(value, "large", StringComparison.Ordinal) ? "large" : "not-large",
        Ambiguity => string.Equals(value, "vague", StringComparison.Ordinal) ? "unclear" : "clear",
        _ => value,
    };

    private static string Instruction(string axis) => axis switch
    {
        IsTask => "The message asks the assistant to change something: write or edit code, files, configuration, or documents. Questions, chat, status reports, pasted logs, and replies confirming or answering the assistant do not count.",
        WantsLoop => "The user explicitly asks for unattended, autonomous iteration until the work is finished (for example 'run until done', 'keep going until it passes', 'do all of them without stopping'). Merely mentioning 'loop' or 'until' in passing, in a quote, or in pasted text does not count.",
        ScopeDefined => "The request names the files, components, or area it applies to.",
        LimitsDefined => "The request states boundaries: what not to touch, a size or count limit, a time box, or constraints on the approach.",
        ApproachDefined => "The request says how the work should be done, not only what.",
        VerificationDefined => "The request states how success will be checked: tests to pass, expected output, or explicit acceptance criteria.",
        IsBugReport => "The message reports something that is broken or behaving wrongly (an error, a crash, a regression, wrong output, a failing test) and wants it fixed. A request for a new feature or a refactor is not a bug report.",
        TouchesUi => "Fulfilling the request would change what a user sees or interacts with on screen: a page, component, layout, style, copy or navigation, in a web app or a mobile app.",
        _ => "Fulfilling the request first requires investigating or reading code or documentation to find something out (a cause, an option, where something lives) before any change can be made.",
    };
}
