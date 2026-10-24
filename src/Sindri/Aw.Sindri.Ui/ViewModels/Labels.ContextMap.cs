using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Context map (F3 spec 1). Plain words for a person who has never read the spec; never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>The intent band's name.</summary>
    internal const string ContextMapBandIntent = "Why we're doing this";

    /// <summary>The product band's name.</summary>
    internal const string ContextMapBandProduct = "What it touches in the product";

    /// <summary>The code band's name.</summary>
    internal const string ContextMapBandCode = "What it touches in the code";

    /// <summary>The intent band's counts.</summary>
    /// <param name="tickets">How many tickets.</param>
    /// <param name="decisions">How many decisions.</param>
    /// <param name="conflicts">How many conflicts (left out at zero).</param>
    /// <returns>"1 ticket · 2 decisions · 1 conflict".</returns>
    internal static string ContextMapMetaIntent(int tickets, int decisions, int conflicts) => Dots(
        Count(tickets, "ticket", "tickets"),
        Count(decisions, "decision", "decisions"),
        conflicts > 0 ? Count(conflicts, "conflict", "conflicts") : null);

    /// <summary>The product band's counts.</summary>
    /// <param name="starts">How many features the read starts at.</param>
    /// <param name="knockOn">How many knock-on effects.</param>
    /// <param name="areas">How many areas in all.</param>
    /// <returns>"Starts at 1 feature · 2 knock-on effects · 9 areas in all".</returns>
    internal static string ContextMapMetaProduct(int starts, int knockOn, int areas) => Dots(
        "Starts at " + Count(starts, "feature", "features"),
        Count(knockOn, "knock-on effect", "knock-on effects"),
        string.Create(CultureInfo.InvariantCulture, $"{areas} {(areas == 1 ? "area" : "areas")} in all"));

    /// <summary>The code band's counts.</summary>
    /// <param name="files">How many files.</param>
    /// <param name="tests">How many of them are tests.</param>
    /// <param name="namesAvailable">Whether the code index served function names.</param>
    /// <returns>"2 files · 0 tests · function names not available yet".</returns>
    internal static string ContextMapMetaCode(int files, int tests, bool namesAvailable) => Dots(
        Count(files, "file", "files"),
        Count(tests, "test", "tests"),
        namesAvailable ? null : "function names not available yet");

    /// <summary>The mark on a node the read starts at.</summary>
    internal const string ContextMapStartsHere = "Starts here";

    /// <summary>The footer of an anchor card.</summary>
    internal const string ContextMapInMap = "In the map";

    /// <summary>The footer's right side of an anchor card.</summary>
    internal const string ContextMapStartsHereFoot = "starts here";

    /// <summary>The footer of a decision in a conflict.</summary>
    internal const string ContextMapConflictFoot = "Conflict";

    /// <summary>The legend's confirmed line.</summary>
    internal const string ContextMapLegendConfirmed = "Confirmed";

    /// <summary>The legend's suggested line.</summary>
    internal const string ContextMapLegendSuggested = "Suggested by AI, not confirmed";

    /// <summary>The pill's words for a suggestion.</summary>
    /// <param name="percent">How sure the tool is, 0 to 100.</param>
    /// <returns>"suggested 74%".</returns>
    internal static string ContextMapSuggestedPct(int percent) => string.Create(CultureInfo.InvariantCulture, $"suggested {percent}%");

    /// <summary>The words on an edge's label pill.</summary>
    /// <param name="type">The served edge type.</param>
    /// <returns>The plain words, or the served type when this build does not know it.</returns>
    internal static string ContextMapEdgeWord(string type) => type switch
    {
        "decides" or "supersedes" => "decided by",
        "mentions" or "quotes" => "mentioned in",
        "conflicts" => "conflicts",
        "implements" => "implemented in",
        "tests" => "tested by",
        "part-of" => "part of",
        "on" => "happens on",
        "reads" => "reads",
        "writes" => "writes",
        "depends-on" => "depends on",
        "step-of" => "step of",
        "moves-to" => "moves to",
        _ => type,
    };

    /// <summary>The word for a node's kind.</summary>
    /// <param name="kind">The served kind, or "ticket", "decision" or "file".</param>
    /// <returns>The plain word, or the served kind when this build does not know it.</returns>
    internal static string ContextMapKindWord(string kind) => kind switch
    {
        "record" or "ticket" => "Ticket",
        "decision" => "Decision",
        "feature" => "Feature",
        "screen" => "Screen",
        "route" => "Route",
        "dialog" => "Dialog",
        "email" or "api" => "API",
        "action" => "Action",
        "data" => "Data",
        "code" or "file" => "File",
        "test" => "Test",
        _ => kind,
    };

    /// <summary>A node's name for assistive technology.</summary>
    /// <param name="kindWord">The kind word.</param>
    /// <param name="label">The node's label.</param>
    /// <returns>"Feature: Tab manager".</returns>
    internal static string ContextMapNodeName(string kindWord, string label) => kindWord + ": " + label;

    /// <summary>The mark on the page head of a draft map.</summary>
    internal const string ContextMapDraftTag = "Draft map";

    /// <summary>The label of the starting points box.</summary>
    internal const string ContextMapStartsAt = "Starts at";

    /// <summary>The label of the pinned or live choice.</summary>
    internal const string ContextMapAsOf = "As of";

    /// <summary>The pinned choice (the map as it was when the plan was approved).</summary>
    internal const string ContextMapAsOfPinned = "At approval";

    /// <summary>The live choice.</summary>
    internal const string ContextMapAsOfLive = "Live";

    /// <summary>The version of the map that answered, as the live choice's hint shows it.</summary>
    /// <param name="version">The map version, "v3".</param>
    /// <param name="commit">The map's commit, or null.</param>
    /// <returns>"Map v3 @ abc1234".</returns>
    internal static string ContextMapVersionLine(string version, string? commit) =>
        commit is { Length: > 0 } ? $"Map {version} @ {commit}" : $"Map {version}";

    /// <summary>Why the pinned choice is off.</summary>
    internal const string ContextMapPinnedNotServed = "Not served yet: the map as it was at approval.";

    /// <summary>The map view's name in the view switch.</summary>
    internal const string ContextMapViewMap = "Map";

    /// <summary>The files view's name in the view switch.</summary>
    internal const string ContextMapViewFiles = "Files";

    /// <summary>The view switch's name for assistive technology.</summary>
    internal const string ContextMapViewName = "View";

    /// <summary>The caption detail of the node the read starts at.</summary>
    internal const string ContextMapAnchorDetail = "where this item starts";

    /// <summary>The coverage area for tickets and decisions.</summary>
    internal const string ContextMapAreaIntent = "Tickets and decisions";

    /// <summary>The coverage area for the product.</summary>
    internal const string ContextMapAreaProduct = "Product";

    /// <summary>The coverage area for the code.</summary>
    internal const string ContextMapAreaCode = "Code";

    /// <summary>The coverage area for function names.</summary>
    internal const string ContextMapAreaNames = "Function names";

    /// <summary>An area that was searched in full.</summary>
    internal const string ContextMapSearched = "searched";

    /// <summary>An area that was searched in part.</summary>
    internal const string ContextMapLimited = "limited";

    /// <summary>An area that could not be searched.</summary>
    internal const string ContextMapNotAvailable = "not available";

    /// <summary>The detail under the function names area when the code index is not built.</summary>
    internal const string ContextMapNamesDetail = "Function names are not available yet: the code index isn't built.";

    /// <summary>The coverage sentence's lead.</summary>
    /// <param name="searched">How many areas were searched in full.</param>
    /// <param name="of">How many areas there are.</param>
    /// <returns>"3 of 4 areas searched.".</returns>
    internal static string ContextMapCoverageLead(int searched, int of) => string.Create(CultureInfo.InvariantCulture, $"{searched} of {of} areas searched.");

    /// <summary>The coverage sentence's rest, naming the areas searched in part.</summary>
    /// <param name="limited">The names of those areas (at least one).</param>
    /// <returns>"Code is limited." or "Product and Code are limited.".</returns>
    internal static string ContextMapCoverageRest(IReadOnlyList<string> limited) => limited.Count switch
    {
        0 => string.Empty,
        1 => limited[0] + " is limited.",
        _ => string.Join(", ", limited.Take(limited.Count - 1)) + " and " + limited[^1] + " are limited.",
    };

    /// <summary>The summary card's title.</summary>
    internal const string ContextMapWhyTitle = "Why this matters";

    /// <summary>The summary card's subtitle.</summary>
    internal const string ContextMapWhySub = "Written from this map";

    /// <summary>The summary card's body while nothing is written.</summary>
    internal const string ContextMapWhyNone = "No summary written for this map yet.";

    /// <summary>The button that closes the summary card.</summary>
    internal const string ContextMapCollapse = "Collapse summary";

    /// <summary>The button that opens the summary card.</summary>
    internal const string ContextMapExpand = "Show summary";

    /// <summary>The zoom in button.</summary>
    internal const string ContextMapZoomIn = "Zoom in";

    /// <summary>The zoom out button.</summary>
    internal const string ContextMapZoomOut = "Zoom out";

    /// <summary>The button that fits the whole map on the stage.</summary>
    internal const string ContextMapZoomFit = "Fit the map";

    /// <summary>The zoom level.</summary>
    /// <param name="scale">The scale, 1 for 100%.</param>
    /// <returns>"91%".</returns>
    internal static string ContextMapZoomPct(double scale) => string.Create(CultureInfo.InvariantCulture, $"{Math.Round(scale * 100, MidpointRounding.AwayFromZero)}%");

    private static string Count(int n, string one, string many) => string.Create(CultureInfo.InvariantCulture, $"{n} {(n == 1 ? one : many)}");

    internal static string Dots(params string?[] parts) => string.Join(" · ", parts.Where(p => !string.IsNullOrEmpty(p)));
}
