using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Todo panel's From elsewhere line (todo spec §3.1, §3.4, §3.5).</summary>
internal static partial class Labels
{
    /// <summary>The line before the first reads answer.</summary>
    internal const string ElsewhereChecking = "Checking reviews and Linear…";

    /// <summary>The line while the views are stalled and Reviews or Linear have not answered: what is stale, not a check that never ends.</summary>
    internal const string ElsewhereStale = "Reviews and Linear wait for Sindri to catch up";

    /// <summary>The line when nothing waits in any of the two places.</summary>
    internal const string ElsewhereNothing = "Nothing waiting elsewhere";

    /// <summary>The Reviews part when its read failed.</summary>
    internal const string ElsewhereReviewsFailed = "Reviews didn't load";

    /// <summary>The Linear part when its read failed.</summary>
    internal const string ElsewhereLinearFailed = "Linear didn't load";

    /// <summary>The Reviews part when no GitHub is connected (a home with no profile yet); it opens Connections.</summary>
    internal const string ElsewhereReviewsNotConnected = "Connect GitHub";

    /// <summary>The Linear part when no Linear source is set up; it opens Connections.</summary>
    internal const string ElsewhereLinearNotConnected = "Connect Linear";

    /// <summary>The button beside a part that failed to load.</summary>
    internal const string ElsewhereRetry = "Retry";

    /// <summary>The line's name for a screen reader.</summary>
    internal const string ElsewhereName = "From elsewhere";

    /// <summary>The Reviews part.</summary>
    /// <param name="count">PRs waiting on the owner's review.</param>
    /// <returns>"2 PRs waiting on your review".</returns>
    internal static string ElsewhereReviews(int count) =>
        string.Create(CultureInfo.InvariantCulture, $"{count} {(count == 1 ? "PR" : "PRs")} waiting on your review");

    /// <summary>The Linear part.</summary>
    /// <param name="count">Open Linear issues assigned to the owner.</param>
    /// <returns>"1 Linear issue assigned".</returns>
    internal static string ElsewhereLinear(int count) =>
        string.Create(CultureInfo.InvariantCulture, $"{count} Linear {(count == 1 ? "issue" : "issues")} assigned");

    /// <summary>A part's name for a screen reader.</summary>
    /// <param name="target">Where it goes: "Reviews", "Linear", "Settings, Connections" or "Needs you".</param>
    /// <param name="text">The part's words.</param>
    /// <returns>"Opens Reviews: 2 PRs waiting on your review".</returns>
    internal static string ElsewhereOpens(string target, string text) => $"Opens {target}: {text}";

    /// <summary>A Retry button's name for a screen reader.</summary>
    /// <param name="what">"Reviews" or "Linear".</param>
    /// <returns>"Retry Reviews".</returns>
    internal static string ElsewhereRetryName(string what) => "Retry " + what;

    /// <summary>The tray tooltip's todo part.</summary>
    /// <param name="overdue">Open todos due before today.</param>
    /// <param name="toSortOut">Todos with an open conflict.</param>
    /// <returns>"2 overdue todos · 1 todo to sort out"; each part only when above zero.</returns>
    internal static string TrayTodos(int overdue, int toSortOut) => string.Join(
        " · ",
        new[]
        {
            overdue > 0 ? string.Create(CultureInfo.InvariantCulture, $"{overdue} overdue {(overdue == 1 ? "todo" : "todos")}") : null,
            toSortOut > 0 ? string.Create(CultureInfo.InvariantCulture, $"{toSortOut} {(toSortOut == 1 ? "todo" : "todos")} to sort out") : null,
        }.OfType<string>());
}
