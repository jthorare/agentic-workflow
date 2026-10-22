namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// The words the app matches on, in one place, each with the field it is compared with. Against a subject's kind: <see cref="Decision"/>
/// (the one test for a decision row, read through <see cref="NeedsYouEntry.DecisionId"/>), <see cref="Service"/> and <see cref="Profile"/>
/// (which details a row shows). Against a row's own kind: <see cref="Decision"/>, <see cref="Failure"/>, <see cref="Profile"/> and
/// <see cref="Park"/> only pick the fallback chip, and <see cref="Park"/> picks the strip's park rows. The queue groups are Home's counts.
/// </summary>
internal static class NeedsYouKinds
{
    /// <summary>A decision: a subject's kind (the decision-row test) and a row's kind (its fallback chip).</summary>
    internal const string Decision = "decision";

    /// <summary>A park (the instance waits on the human): a row's kind.</summary>
    internal const string Park = "park";

    /// <summary>A service (a failure row): a subject's kind.</summary>
    internal const string Service = "service";

    /// <summary>The profile (a drift row): a subject's kind and a row's kind.</summary>
    internal const string Profile = "profile";

    /// <summary>A changed workflow (a recipe awaiting its own approval): a subject's kind; its id is the recipe's.</summary>
    internal const string Recipe = "recipe";

    /// <summary>A failure: a row's kind.</summary>
    internal const string Failure = "failure";

    /// <summary>The queue's running group.</summary>
    internal const string Running = "running";

    /// <summary>The queue's with-you group.</summary>
    internal const string WithYou = "withYou";

    /// <summary>The queue's queued group.</summary>
    internal const string Queued = "queued";
}
