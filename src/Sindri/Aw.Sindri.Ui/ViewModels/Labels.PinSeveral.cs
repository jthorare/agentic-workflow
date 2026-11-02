using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Add several sheet (concept pins spec §3.6, §5).</summary>
internal static partial class Labels
{
    /// <summary>The sheet's heading.</summary>
    internal const string PinSeveralTitle = "Add several";

    /// <summary>The list box's accessible name.</summary>
    internal const string PinSeveralBox = "Your list of concepts";

    /// <summary>The box's placeholder.</summary>
    internal const string PinSeveralHint = "One concept per line, or a block of lines with a blank line between blocks.";

    /// <summary>The button that turns the box into rows.</summary>
    internal const string PinSeveralRead = "Read the list";

    /// <summary>A list with more concepts than one write takes.</summary>
    internal const string PinSeveralTooMany = "Up to 100 at a time; the first 100 are read.";

    /// <summary>A row while the suggest read runs.</summary>
    internal const string PinSeveralMatching = "Matching your list…";

    /// <summary>A row with no matched concept, and the picker's first choice.</summary>
    internal const string PinSeveralNoMatch = "New, no match yet";

    /// <summary>A row when the suggest read failed.</summary>
    internal const string PinSeveralMatchFailed = "Couldn't match against the map just now; you can still pin them as new.";

    /// <summary>A line with no name or a name over 80 characters.</summary>
    internal const string PinSeveralUnreadable = "Couldn't read this line";

    /// <summary>The button when no row is ticked.</summary>
    internal const string PinSeveralTickOne = "Tick at least one line.";

    /// <summary>The write failed for a reason with no sentence of its own.</summary>
    internal const string PinSeveralFailed = "Couldn't write the pins just now. Try again.";

    /// <summary>A word that comes after the 20th.</summary>
    internal const string PinSeveralOverWords = "Only 20 words are kept; this one is left out";

    /// <summary>The row's name box.</summary>
    internal const string PinSeveralNameBox = "Name";

    /// <summary>The row's definition box.</summary>
    internal const string PinSeveralDefinitionBox = "Definition";

    /// <summary>The row's match picker.</summary>
    internal const string PinSeveralPicker = "Matches the map's concept";

    /// <summary>The button that writes every ticked row.</summary>
    internal static string PinSeveralSubmit(bool approve) => approve ? "Pin all and approve" : "Pin all";

    /// <summary>The button while the write runs.</summary>
    internal static string PinSeveralSubmitting(int count) => string.Create(CultureInfo.InvariantCulture, $"Pinning {count}…");

    /// <summary>A row whose name is a pin's name or id.</summary>
    internal static string PinSeveralUpdates(string pin) => "Updates your pin " + pin;

    /// <summary>A row repeating an earlier row's name.</summary>
    internal static string PinSeveralSameName(int line) => string.Create(CultureInfo.InvariantCulture, $"Same name as line {line}; only one is kept");

    /// <summary>A row matched to the concept an earlier row is matched to.</summary>
    internal static string PinSeveralAlsoMatched(string concept) => "Also matched to " + concept + "; pick another or New";

    /// <summary>The row's place: "line 3".</summary>
    internal static string PinSeveralLine(int number) => string.Create(CultureInfo.InvariantCulture, $"line {number}");

    /// <summary>How the row split its line.</summary>
    internal static string PinSeveralSplit(string name, string? definition) =>
        "Name: " + name + (string.IsNullOrEmpty(definition) ? string.Empty : " · Definition: " + (definition.Length > 60 ? definition[..60].TrimEnd() + "…" : definition));

    /// <summary>The row as a screen reader reads it.</summary>
    internal static string PinSeveralRowName(int number, string name, string? match) =>
        PinSeveralLine(number) + ", " + (name.Length == 0 ? PinSeveralUnreadable : name) + (match is null ? string.Empty : ", matched to " + match);

    /// <summary>The features a row will pin.</summary>
    internal static string PinSeveralFeatures(IEnumerable<string> labels) => "Features: " + string.Join(", ", labels);
}
