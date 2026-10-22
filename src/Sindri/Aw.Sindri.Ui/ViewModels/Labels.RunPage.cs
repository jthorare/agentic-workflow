using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the run page (F3 spec 3 §6, slice 3). Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>The runs sidebar's eyebrow.</summary>
    internal const string RunsTitle = "Runs";

    /// <summary>The runs search box's name and hint.</summary>
    internal const string RunsSearch = "Search runs";

    /// <summary>The line under the runs list while only open runs are listed.</summary>
    internal const string RunsOpenOnly = "Only runs that are still open are listed here.";

    /// <summary>The runs list when no run matches.</summary>
    internal const string RunsNone = "No runs match.";

    /// <summary>The page's name for screen readers.</summary>
    internal const string RunPageName = "Run";

    /// <summary>The loading line.</summary>
    internal const string RunLoading = "Reading the run";

    /// <summary>The header's Back button.</summary>
    internal const string RunBack = "Back to Workflows";

    /// <summary>The words of the zoom pill's buttons.</summary>
    internal const string RunZoomIn = "Zoom in";

    /// <summary>Zoom out.</summary>
    internal const string RunZoomOut = "Zoom out";

    /// <summary>Fit the run.</summary>
    internal const string RunZoomFit = "Fit the run";

    /// <summary>Centre on the active step.</summary>
    internal const string RunZoomCentre = "Centre on the active step";

    /// <summary>The minimap's name.</summary>
    internal const string RunMinimapName = "Overview of the run";

    /// <summary>The spot the inspector (slice 4) fills.</summary>
    internal const string RunInspectorName = "Step details";

    /// <summary>The header's copy-reference button.</summary>
    internal const string RunCopyReference = "Copy reference";

    /// <summary>"Started 9h 35m ago".</summary>
    /// <param name="ago">How long ago, as <see cref="Duration(long)"/> says it.</param>
    /// <returns>The words.</returns>
    internal static string RunStarted(string ago) => $"Started {ago} ago";

    /// <summary>"Stage 2 of 4".</summary>
    /// <param name="n">The stage's place.</param>
    /// <param name="total">How many stages.</param>
    /// <returns>The words.</returns>
    internal static string StageOf(int n, int total) => string.Create(CultureInfo.InvariantCulture, $"Stage {n} of {total}");

    /// <summary>"Plan stage, step 2 of 5".</summary>
    /// <param name="stage">The stage's name.</param>
    /// <param name="i">The step's place.</param>
    /// <param name="n">How many steps.</param>
    /// <returns>The words.</returns>
    internal static string StepOf(string stage, int i, int n) => string.Create(CultureInfo.InvariantCulture, $"{stage} stage, step {i} of {n}");

    /// <summary>"Try 2 of 3", or "Try 2" when there is no limit.</summary>
    /// <param name="n">The try.</param>
    /// <param name="max">The most tries, or null.</param>
    /// <returns>The words.</returns>
    internal static string StepTry(int n, int? max) =>
        max is { } limit ? string.Create(CultureInfo.InvariantCulture, $"Try {n} of {limit}") : string.Create(CultureInfo.InvariantCulture, $"Try {n}");

    /// <summary>The stage footer's count of child runs done.</summary>
    /// <param name="done">How many are done.</param>
    /// <param name="total">How many there are.</param>
    /// <returns>"1 of 3 done".</returns>
    internal static string RunPhasesDone(int done, int total) => string.Create(CultureInfo.InvariantCulture, $"{done} of {total} done");

    /// <summary>A stage card's help line for assistive tech: where it leads.</summary>
    /// <param name="next">The next stage's name.</param>
    /// <returns>"Leads to Design".</returns>
    internal static string RunLeadsTo(string next) => $"Leads to {next}";

    /// <summary>The name of a runs-list card.</summary>
    /// <param name="title">The run's title.</param>
    /// <param name="word">Its state word.</param>
    /// <returns>The words.</returns>
    internal static string RunCardName(string title, string word) => $"{title}, {word}";
}
