using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Needs-you panel's seen state and motion words.</summary>
internal static partial class Labels
{
    /// <summary>The word next to an unseen pulse row under reduced motion (UI spec §5).</summary>
    internal const string New = "new";

    /// <summary>A strip row that is an incident, in its accessible name (the glyph is not spoken).</summary>
    internal const string Incident = "incident";

    /// <summary>The urgency word of a pulse row in its accessible name (UI spec §11.3).</summary>
    internal const string UrgencyPulse = "needs you now";

    /// <summary>The urgency word of a colour (failure) row.</summary>
    internal const string UrgencyColour = "failed";

    /// <summary>The urgency word of a calm row.</summary>
    internal const string UrgencyCalm = "to look at";

    /// <summary>The accessible name of a Needs-you row: the urgency word, the chip, the title and the id, and "new" while it is unseen.</summary>
    /// <param name="urgency">The row's urgency word.</param>
    /// <param name="chip">The chip's word; empty for none.</param>
    /// <param name="title">The row's title.</param>
    /// <param name="id">The id the row shows.</param>
    /// <param name="isNew">True while the row is unseen.</param>
    /// <returns>The name.</returns>
    internal static string NeedsYouRowName(string urgency, string chip, string title, string id, bool isNew) =>
        string.Join(", ", new[] { urgency, chip, title, id, isNew ? New : "" }.Where(part => part.Length > 0));

    /// <summary>The Needs-you row's title button: opens the row's sheet.</summary>
    /// <param name="title">The row's title.</param>
    /// <param name="id">The id the row shows.</param>
    /// <returns>The name.</returns>
    internal static string OpenRow(string title, string id) => $"Open {title} {id}";

    /// <summary>The Needs-you panel's Mark all seen button.</summary>
    internal const string MarkAllSeen = "Mark all seen";

    /// <summary>What Decide did, spoken and shown once it is accepted.</summary>
    /// <param name="label">The option's label.</param>
    /// <returns>The line.</returns>
    internal static string Decided(string label) => "Decided: " + label;

    /// <summary>The Undo button after Mark all seen.</summary>
    internal const string Undo = "Undo";

    /// <summary>The Undo button's accessible name: what it undoes (a list of buttons has several "Undo").</summary>
    internal const string UndoMarkAllSeen = "Undo mark all seen";

    /// <summary>The decision sheet's note field: the caption, its accessible name, and what it does.</summary>
    internal const string DecisionNoteLabel = "Note, sent with your choice";

    /// <summary>What Mark all seen did, before its Undo button (mockup comment, provisional owner default 2026-10-14 night).</summary>
    /// <param name="count">How many rows it marked.</param>
    /// <returns>The line.</returns>
    internal static string MarkedSeen(int count) => string.Create(CultureInfo.InvariantCulture, $"{count} marked seen");

    /// <summary>The tooltip of an action that reads the views while they rebuild (UI spec §4.1, SND-API-010).</summary>
    internal const string RetryAfterRebuild = "Retry after the rebuild";

    /// <summary>The Needs-you panel's overflow control; it expands the list in place (D8: the "all N →" form of the mockups).</summary>
    /// <param name="count">How many rows there are.</param>
    /// <returns>The words.</returns>
    internal static string AllRows(int count) => string.Create(CultureInfo.InvariantCulture, $"all {count} →");

    /// <summary>A row's age: minutes under an hour, then hours, then days.</summary>
    /// <param name="age">How long the row has waited.</param>
    /// <returns>The words, such as "12m", "2h", "3d".</returns>
    internal static string RowAge(TimeSpan age) => age.TotalMinutes switch
    {
        < 1 => "<1m",
        < 60 => string.Create(CultureInfo.InvariantCulture, $"{(int)age.TotalMinutes}m"),
        < 1440 => string.Create(CultureInfo.InvariantCulture, $"{(int)age.TotalHours}h"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{(int)age.TotalDays}d"),
    };

    // The attention summary: window title, tray tooltips and menu, the summary line, the live region (UI spec §5, §12.4).

    /// <summary>The window title while rows need the user.</summary>
    /// <param name="count">How many rows.</param>
    /// <returns>"(N) Needs you · Sindri".</returns>
    internal static string TitleNeedsYou(int count) => string.Create(CultureInfo.InvariantCulture, $"({count}) Needs you · {Sindri}");

    /// <summary>The tray's tooltip while a pulse or colour row exists.</summary>
    /// <param name="count">How many rows need the user.</param>
    /// <param name="running">How many items run.</param>
    /// <returns>The words.</returns>
    internal static string TrayNeedYou(int count, int running) => string.Create(CultureInfo.InvariantCulture, $"Sindri · {count} need you · {running} running");

    /// <summary>The tray's tooltip while nothing needs the user.</summary>
    /// <param name="running">How many items run.</param>
    /// <returns>The words.</returns>
    internal static string TrayNothingNeedsYou(int running) => string.Create(CultureInfo.InvariantCulture, $"Sindri · {running} running · nothing needs you");

    /// <summary>The tray's tooltip while only calm rows exist.</summary>
    /// <param name="count">How many rows.</param>
    /// <returns>The words.</returns>
    internal static string TrayToLookAt(int count) => string.Create(CultureInfo.InvariantCulture, $"Sindri · {count} to look at");

    /// <summary>The tray menu's disabled running item.</summary>
    /// <param name="running">How many items run.</param>
    /// <returns>The words.</returns>
    internal static string TrayRunningItem(int running) => string.Create(CultureInfo.InvariantCulture, $"Running: {running}");

    /// <summary>One needs-you row in the tray menu: chip word, id, title and age.</summary>
    /// <param name="chip">The chip word.</param>
    /// <param name="id">The id shown at rest (or the row id).</param>
    /// <param name="title">The title.</param>
    /// <param name="age">The age.</param>
    /// <returns>The label.</returns>
    internal static string TrayRow(string chip, string id, string title, string age) =>
        string.Join(' ', new[] { chip, id, title }.Where(part => part.Length > 0)) + (age.Length > 0 ? " · " + age : "");

    /// <summary>Home's summary line.</summary>
    /// <param name="needYou">Rows that need the user.</param>
    /// <param name="running">Items running.</param>
    /// <param name="withYou">Items the user took over.</param>
    /// <param name="queued">Items queued.</param>
    /// <returns>The words.</returns>
    internal static string Summary(int needYou, int running, int withYou, int queued) =>
        string.Create(CultureInfo.InvariantCulture, $"{needYou} need you · {running} running · {withYou} with you · {queued} queued");

    /// <summary>The live region's line when rows arrive.</summary>
    /// <param name="count">How many rows need the user now.</param>
    /// <returns>The words.</returns>
    internal static string NowNeedYou(int count) => string.Create(CultureInfo.InvariantCulture, $"{count} now need you");

    /// <summary>A failure sheet's line for the service.</summary>
    internal static string SheetService => "Service";

    /// <summary>A failure sheet's line for how long ago it failed.</summary>
    internal static string SheetFailed => "Failed";

    /// <summary>A failure sheet's line for why.</summary>
    internal static string SheetWhy => "Why";

    /// <summary>A profile sheet's line for what changed.</summary>
    internal static string SheetChanged => "Changed";

    /// <summary>A profile sheet's line for the profile's hash.</summary>
    internal static string SheetHash => "Fingerprint";

    /// <summary>A profile sheet's line for the command that approves it.</summary>
    internal static string SheetApprove => "To approve, run";

    /// <summary>The words that name the placeholders a command still has open.</summary>
    /// <param name="names">The open placeholders, without their angle brackets.</param>
    /// <returns>For example "fill in name".</returns>
    internal static string FillInHint(IReadOnlyList<string> names) => "fill in " + string.Join(", ", names);

    /// <summary>A command with its fill-in hint, for display and spoken text only; a copy takes the command alone.</summary>
    /// <param name="cli">The command, placeholder and all.</param>
    /// <param name="hint">The hint, or empty.</param>
    /// <returns>The command, then the hint after a middle dot when there is one.</returns>
    internal static string WithHint(string cli, string hint) => hint.Length > 0 ? cli + " · " + hint : cli;

    /// <summary>An action's value on a sheet while the app cannot run it yet.</summary>
    /// <param name="cli">The command that does it.</param>
    /// <returns>The words.</returns>
    internal static string NotInTheAppYet(string cli) => cli.Length > 0 ? "Not in the app yet · " + cli : "Not in the app yet";

    /// <summary>The item sheet's heading for the item's pending decisions.</summary>
    internal static string ItemDecisions => "Decisions";

    /// <summary>The item sheet's heading for the requests filed against the item.</summary>
    internal static string ItemRequests => "Requests";
}
