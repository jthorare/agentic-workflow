using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Context page's Files view and its states (F3 spec 1, slice 4). Plain words; never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>The files table's first column.</summary>
    internal const string ContextFilesColFile = "File";

    /// <summary>The files table's second column.</summary>
    internal const string ContextFilesColFor = "What it is for";

    /// <summary>The name of the top folder.</summary>
    internal const string ContextFilesRoot = "Top of the repo";

    /// <summary>The button that goes up one folder.</summary>
    internal const string ContextFilesUp = "Up one folder";

    /// <summary>The word while a folder is read.</summary>
    internal const string ContextFilesLoading = "Reading the files...";

    /// <summary>The word for a folder with nothing in it.</summary>
    internal const string ContextFilesEmpty = "Nothing in this folder.";

    /// <summary>What a file with no feature says.</summary>
    internal const string ContextFilesNoFeature = "Not linked to a feature yet";

    /// <summary>What a file with no ticket or decision says.</summary>
    internal const string ContextFilesIntentNone = "None";

    /// <summary>The word for a file the tool suggests a link for.</summary>
    internal const string ContextFilesSuggested = "Suggested, not confirmed";

    /// <summary>The word for a file with no link.</summary>
    internal const string ContextFilesUnmapped = "Not in the map";

    /// <summary>How many files a folder holds.</summary>
    /// <param name="files">The count.</param>
    /// <returns>"4 files".</returns>
    internal static string ContextFilesCount(int files) => Count(files, "file", "files");

    /// <summary>A row's tickets and decisions.</summary>
    /// <param name="records">How many tickets.</param>
    /// <param name="decisions">How many decisions.</param>
    /// <returns>"1 ticket · 2 decisions", or <see cref="ContextFilesIntentNone"/>.</returns>
    internal static string ContextFilesIntent(int records, int decisions) => records == 0 && decisions == 0
        ? ContextFilesIntentNone
        : Dots(records > 0 ? Count(records, "ticket", "tickets") : null, decisions > 0 ? Count(decisions, "decision", "decisions") : null);

    /// <summary>The totals line over the table.</summary>
    /// <param name="files">All files.</param>
    /// <param name="mapped">Files in the map.</param>
    /// <param name="suggested">Files with a suggested link.</param>
    /// <param name="unmapped">Files not in the map.</param>
    /// <returns>"3 files · 1 in the map · 1 suggested · 1 not in the map".</returns>
    internal static string ContextFilesTotals(int files, int mapped, int suggested, int unmapped) => Dots(
        Count(files, "file", "files"),
        string.Create(CultureInfo.InvariantCulture, $"{mapped} in the map"),
        string.Create(CultureInfo.InvariantCulture, $"{suggested} suggested"),
        string.Create(CultureInfo.InvariantCulture, $"{unmapped} not in the map"));

    /// <summary>A feature key as a person reads it.</summary>
    /// <param name="key">The key, "feature:tabs.tab-groups".</param>
    /// <returns>"tabs.tab-groups" (the key without its kind word).</returns>
    internal static string ContextFilesFeature(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        var colon = key.IndexOf(':', StringComparison.Ordinal);
        return colon >= 0 && colon < key.Length - 1 ? key[(colon + 1)..] : key;
    }

    /// <summary>The words for a row's status; a status this build does not know shows as served.</summary>
    /// <param name="status">The status as served.</param>
    /// <returns>The words.</returns>
    internal static string ContextFilesStatus(string status) => status switch
    {
        "mapped" => ContextMapInMap,
        "suggested" => ContextFilesSuggested,
        "unmapped" => ContextFilesUnmapped,
        _ => status,
    };

    /// <summary>The Building state's title.</summary>
    internal const string ContextStateBuilding = "Building the map for this item...";

    /// <summary>The Building state's body.</summary>
    internal const string ContextStateBuildingBody = "Reading the ticket, the product map and the code. This usually takes under a minute.";

    /// <summary>The No map state's title.</summary>
    internal const string ContextStateEmptyTitle = "No map for this item yet";

    /// <summary>The No map state's body.</summary>
    internal const string ContextStateEmptyBody = "Build it to see why this work exists and what it touches.";
}
