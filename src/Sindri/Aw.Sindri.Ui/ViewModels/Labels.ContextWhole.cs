using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Context page's whole map (whole-map spec §3).</summary>
internal static partial class Labels
{
    /// <summary>The head over the whole map.</summary>
    /// <param name="version">The map version ('v3').</param>
    /// <returns>"Context · the whole map · v3".</returns>
    internal static string ContextWholeHead(string version) => "Context · the whole map · " + version;

    /// <summary>The last step of the map's breadcrumb while a query's answer is on it.</summary>
    internal const string ContextCrumbAnswer = "Starting points";

    /// <summary>The map breadcrumb's accessible name.</summary>
    internal const string ContextMapCrumbsName = "Where you are on the map";

    /// <summary>What a feature cut at the cap shows.</summary>
    /// <param name="shown">How many items are drawn.</param>
    /// <param name="total">How many the feature holds.</param>
    /// <returns>"Showing 500 of 640 items; the rest are not shown."</returns>
    internal static string ContextWholeAreaTruncated(int shown, int total) =>
        string.Create(CultureInfo.InvariantCulture, $"Showing {shown} of {total} items; the rest are not shown.");

    /// <summary>The note by the search box when nothing on the map matches.</summary>
    internal const string ContextSearchNoMatches = "No matches";

    /// <summary>The button that empties the search box and returns to the full map.</summary>
    internal const string ContextSearchClear = "Clear search";

    /// <summary>The line when what was asked is not on the whole map.</summary>
    internal const string ContextWholeNoMatch = "None of those items are on the map.";

    /// <summary>The chip when more features were touched than were opened.</summary>
    /// <param name="more">How many features were not opened.</param>
    /// <returns>"2 more features touched".</returns>
    internal static string ContextWholeMoreTouched(int more) => string.Create(CultureInfo.InvariantCulture, $"{more} more {(more == 1 ? "feature" : "features")} touched");

    /// <summary>The clause on a band's label for what stays a tile and is not drawn as nodes.</summary>
    /// <param name="more">How many items the tiles hold in that band.</param>
    /// <param name="where">The feature, or "3 features".</param>
    /// <returns>"168 more in Billing".</returns>
    internal static string ContextWholeBandMore(int more, string where) => string.Create(CultureInfo.InvariantCulture, $"{more} more in {where}");

    /// <summary>The line over the stage while the map is read.</summary>
    internal const string ContextWholeLoading = "Reading the map…";

    /// <summary>The line when the open-time read failed.</summary>
    internal const string ContextWholeFailed = "Couldn't read the map";

    /// <summary>The button that reads the map again.</summary>
    internal const string ContextWholeRetry = "Retry";

    /// <summary>The size of the map and when it was built.</summary>
    /// <param name="items">Nodes.</param>
    /// <param name="links">Edges.</param>
    /// <param name="builtAt">When it was built, in local time.</param>
    /// <returns>"412 items · 655 links · built 23 Oct 2026 06:00".</returns>
    internal static string ContextWholeSize(int items, int links, DateTimeOffset builtAt) =>
        string.Create(CultureInfo.InvariantCulture, $"{items} items · {links} links · built {builtAt:d MMM yyyy HH:mm}");

    /// <summary>A tile's count.</summary>
    /// <param name="members">How many items.</param>
    /// <returns>"34 items".</returns>
    internal static string ContextWholeMembers(int members) => string.Create(CultureInfo.InvariantCulture, $"{members} {(members == 1 ? "item" : "items")}");

    /// <summary>A tile's accessible name.</summary>
    /// <param name="label">The feature's label.</param>
    /// <param name="members">How many items.</param>
    /// <returns>"Billing, 34 items. Press Enter to open."</returns>
    internal static string ContextWholeTileName(string label, int members) => $"{label}, {ContextWholeMembers(members)}. Press Enter to open.";

    /// <summary>The map's accessible name.</summary>
    internal const string ContextWholeCanvasName = "The whole map";

    /// <summary>The map canvas's accessible name once bands are drawn: the bands top to bottom, each with what it holds.</summary>
    /// <param name="bands">Each band's title and what it holds ("2 decisions · 3 records"; empty for nothing).</param>
    /// <returns>"The whole map: Why (2 decisions), Product (2 features), Code (2 code files)".</returns>
    internal static string ContextWholeCanvasBands(IEnumerable<(string Title, string Meta)> bands) =>
        $"{ContextWholeCanvasName}: {string.Join(", ", bands.Select(b => b.Meta.Length > 0 ? $"{b.Title} ({b.Meta})" : b.Title))}";

    /// <summary>A kind and its count, in people's words.</summary>
    /// <param name="kind">The node kind.</param>
    /// <param name="count">How many.</param>
    /// <returns>"3 features", "5 code files".</returns>
    internal static string ContextWholeKindCount(string kind, int count) => string.Create(CultureInfo.InvariantCulture, $"{count} {KindWord(kind, count)}");

    private static string KindWord(string kind, int count) => (kind, count == 1) switch
    {
        ("feature", true) => "feature",
        ("feature", false) => "features",
        ("surface", true) => "screen",
        ("surface", false) => "screens",
        ("action", true) => "action",
        ("action", false) => "actions",
        ("thing", true) => "thing",
        ("thing", false) => "things",
        ("flow", true) => "flow",
        ("flow", false) => "flows",
        ("decision", true) => "decision",
        ("decision", false) => "decisions",
        ("record", true) => "record",
        ("record", false) => "records",
        ("code", true) => "code file",
        ("code", false) => "code files",
        (_, _) => kind,
    };
}
