namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// The words the app matches on, in one place, each with the field it is compared with. Against a subject's kind: <see cref="Decision"/>
/// (the one test for a decision row, read through <see cref="NeedsYouEntry.DecisionId"/>), <see cref="Service"/> and <see cref="Profile"/>
/// (which details a row shows). Against a row's own kind: <see cref="Decision"/>, <see cref="Failure"/> and <see cref="Profile"/> only pick the fallback chip. The queue groups are Home's counts.
/// </summary>
internal static class NeedsYouKinds
{
    /// <summary>A decision: a subject's kind (the decision-row test) and a row's kind (its fallback chip).</summary>
    internal const string Decision = "decision";

    /// <summary>A service (a failure row): a subject's kind.</summary>
    internal const string Service = "service";

    /// <summary>The profile (a drift row): a subject's kind and a row's kind.</summary>
    internal const string Profile = "profile";

    /// <summary>A failure: a row's kind.</summary>
    internal const string Failure = "failure";

    /// <summary>The kinds of row a server from before the recipe engine went still sends (a desk task, a parked run, a skill wait, a changed recipe): the app does not show them.</summary>
    internal static readonly string[] Retired = ["desk", "park", "skillWaiting", "recipe"];

    /// <summary>The queue's running group.</summary>
    internal const string Running = "running";

    /// <summary>The queue's with-you group.</summary>
    internal const string WithYou = "withYou";

    /// <summary>The queue's queued group.</summary>
    internal const string Queued = "queued";
}
