using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Context page's concept band, the first layer of the map.</summary>
internal static partial class Labels
{
    /// <summary>The head over the concept layer.</summary>
    internal const string ContextConceptsHead = "Context · concepts";

    /// <summary>The first step of the breadcrumb, and the layer's accessible name.</summary>
    internal const string ContextConceptsCrumb = "Concepts";

    /// <summary>The list of concepts' accessible name.</summary>
    internal const string ContextConceptsListName = "The product's concepts";

    /// <summary>The button that goes up one layer.</summary>
    internal const string ContextConceptsBack = "Back";

    /// <summary>The mark on a concept a person set.</summary>
    internal const string ContextConceptSetByPerson = "Set by a person";

    /// <summary>The line when the map has no concepts yet; the map itself opens as before.</summary>
    internal const string ContextConceptsEmpty = "The map has no concepts yet, so the whole map is shown.";

    /// <summary>The line over the cards when no concept has terms yet.</summary>
    internal const string ContextConceptsNoTerms = "No terms yet.";

    /// <summary>The line over the cards when the glossary is refused.</summary>
    /// <param name="code">The error code.</param>
    /// <param name="message">The refusal's message.</param>
    /// <returns>"Terms are not available (SND-CONTEXT-041): ...".</returns>
    internal static string ContextConceptsTermsUnavailable(string code, string message) => $"Terms are not available ({code}): {message}";

    /// <summary>The line over the concepts when the open concept is gone from a newer map version.</summary>
    internal static string ContextConceptGone(string name, int version) => string.Create(CultureInfo.InvariantCulture, $"{name} isn't in map v{version} any more");

    /// <summary>The line while the concepts are still being built; the map itself opens as before.</summary>
    internal const string ContextConceptsBuilding = "The concepts are still being built, so the whole map is shown.";

    /// <summary>The line when the concepts cannot be read; the map itself opens as before.</summary>
    internal const string ContextConceptsUnavailable = "No concepts yet, showing the whole map.";

    /// <summary>The line when the concepts read failed (the code is in its tooltip); the map itself opens as before.</summary>
    internal const string ContextConceptsReadFailed = "Concepts can't be read right now, showing the whole map.";

    /// <summary>What a concept row counts: files, the one unit (#3677).</summary>
    /// <param name="files">How many files the concept holds.</param>
    /// <returns>"40 files", "1 file", or "no files yet".</returns>
    internal static string ContextConceptFiles(int files) => files switch
    {
        <= 0 => "no files yet",
        1 => "1 file",
        _ => string.Create(CultureInfo.InvariantCulture, $"{files} files"),
    };

    /// <summary>When a concept's newest file changed.</summary>
    /// <param name="at">The time.</param>
    /// <param name="zone">The zone the day is read in.</param>
    /// <returns>"changed 13 Nov".</returns>
    internal static string ContextConceptChanged(DateTimeOffset at, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        return "changed " + TimeZoneInfo.ConvertTime(at, zone).ToString("d MMM", CultureInfo.InvariantCulture);
    }

    /// <summary>The line on a concept card when a rebuild found suggested pages the owner has not opened yet (pages spec §3.5).</summary>
    /// <param name="count">How many are new.</param>
    /// <returns>"1 new suggested page" or "2 new suggested pages".</returns>
    internal static string ContextConceptNewSuggested(int count) => string.Create(CultureInfo.InvariantCulture, $"{count} new suggested {(count == 1 ? "page" : "pages")}");

    /// <summary>A concept card's top terms.</summary>
    /// <param name="shown">The terms to name, best first.</param>
    /// <param name="more">How many more there are.</param>
    /// <returns>"Terms: shift · swap · roster +2".</returns>
    internal static string ContextConceptTerms(IReadOnlyList<string> shown, int more)
    {
        ArgumentNullException.ThrowIfNull(shown);
        return "Terms: " + string.Join(" · ", shown) + (more > 0 ? " +" + more.ToString(CultureInfo.InvariantCulture) : string.Empty);
    }

    /// <summary>A concept's accessible name.</summary>
    /// <param name="name">The concept's name.</param>
    /// <param name="size">The size line.</param>
    /// <returns>"Self scheduling, 4 features. Press Enter to open."</returns>
    internal static string ContextConceptTileName(string name, string size) => $"{name}, {size}. Press Enter to open.";

    /// <summary>The segment that draws the links from a drilled band to the other bands.</summary>
    internal const string ContextDrillCross = "Links to other bands";

    /// <summary>The segment that draws a drilled band's own links.</summary>
    /// <param name="band">The band's title.</param>
    /// <returns>"Links inside Why".</returns>
    internal static string ContextDrillOwn(string band) => $"Links inside {band}";

    /// <summary>The button over a concept's map that drills into a band.</summary>
    /// <param name="band">The band's title.</param>
    /// <returns>"Open the Why band".</returns>
    internal static string ContextDrillOpen(string band) => $"Open the {band} band";

    /// <summary>The line when a drilled band has no links to the other bands.</summary>
    /// <param name="band">The band's title.</param>
    /// <returns>"No links from Why to the other bands."</returns>
    internal static string ContextDrillNoCross(string band) => $"No links from {band} to the other bands.";

    /// <summary>The band header note under the Product band of the Why drill when nothing in Product is linked to what the Why band explains.</summary>
    /// <returns>"None linked yet".</returns>
    internal static string ContextDrillWhyNoProduct => "None linked yet";

    /// <summary>The line under the Why drill when no page, feature or action is linked to what the Why band explains.</summary>
    /// <returns>"No pages, features or actions are linked to these records and decisions yet."</returns>
    internal static string ContextDrillWhyNoProductLine => "No pages, features or actions are linked to these records and decisions yet.";

    /// <summary>The line when a drilled band has no links of its own.</summary>
    /// <param name="band">The band's title.</param>
    /// <returns>"No links inside Why."</returns>
    internal static string ContextDrillNoOwn(string band) => $"No links inside {band}.";
}
