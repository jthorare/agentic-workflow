using System.Globalization;
using Aw.Kernel;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Memory page (stores, search, write fact, forget). Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>The page's name.</summary>
    internal const string MemoryPage = "Memory";

    /// <summary>The page's one-line purpose.</summary>
    internal const string MemoryPurpose = "Notes Sindri and your agents keep between sessions. Search a store to see its facts.";

    /// <summary>The page before its first answer.</summary>
    internal const string ReadingMemory = "Reading memory stores…";

    /// <summary>The page when an answer can't be read.</summary>
    internal const string MemoryUnreadable = "The memory list can't be read from this Sindri.";

    /// <summary>The page when there are no stores.</summary>
    internal const string MemoryEmpty = "No memory stores yet. Create one below.";

    /// <summary>The page when the first read failed: the reason follows.</summary>
    internal static string MemoryFailed(Error error) => "Couldn't read the memory stores just now. " + InstanceRefused(error);

    /// <summary>The head: "2 stores · 12 facts".</summary>
    internal static string MemoryHead(int stores, int facts) =>
        string.Create(CultureInfo.InvariantCulture, $"{stores} {(stores == 1 ? "store" : "stores")} · {Facts(facts)}");

    /// <summary>"1 fact" or "3 facts".</summary>
    internal static string Facts(int count) => string.Create(CultureInfo.InvariantCulture, $"{count} {(count == 1 ? "fact" : "facts")}");

    /// <summary>The create form's heading.</summary>
    internal const string MemoryCreateHeading = "New memory store";

    /// <summary>The create form's name field.</summary>
    internal const string MemoryNameField = "Store name";

    /// <summary>The create form's description field.</summary>
    internal const string MemoryDescriptionField = "What it is for (optional)";

    /// <summary>The create action.</summary>
    internal const string MemoryCreate = "Create store";

    /// <summary>A create with no name.</summary>
    internal const string MemoryNameNeeded = "Give the store a name first.";

    /// <summary>A create that failed: the reason follows.</summary>
    internal static string MemoryCreateFailed(Error error) => "Couldn't create the store. " + InstanceRefused(error);

    /// <summary>The control that opens a store.</summary>
    internal const string MemoryOpen = "Open";

    /// <summary>The control that closes a store.</summary>
    internal const string MemoryClose = "Close";

    /// <summary>The search box's accessible name.</summary>
    internal const string MemorySearchField = "Search this store";

    /// <summary>The search action.</summary>
    internal const string MemorySearchAction = "Search";

    /// <summary>The match level's accessible name.</summary>
    internal const string MemoryLevelField = "Match level";

    /// <summary>The match levels, loosest first.</summary>
    internal static readonly string[] MemoryLevels = ["loose", "balanced", "strict"];

    /// <summary>The level a search starts at.</summary>
    internal const string MemoryDefaultLevel = "balanced";

    /// <summary>The chip for a semantic answer.</summary>
    internal const string MemoryModeSemantic = "Semantic match";

    /// <summary>The chip for a keyword answer.</summary>
    internal const string MemoryModeKeyword = "Keyword match";

    /// <summary>The line for a search that found nothing.</summary>
    internal const string MemoryNothingFound = "Nothing matched.";

    /// <summary>The line for a search that found facts.</summary>
    internal static string MemoryFound(int count) => Facts(count) + " found";

    /// <summary>A search that failed: the reason follows.</summary>
    internal static string MemorySearchFailed(Error error) => "Couldn't search the store. " + InstanceRefused(error);

    /// <summary>The write form's heading.</summary>
    internal const string MemoryWriteHeading = "Write a fact";

    /// <summary>The write form's text field.</summary>
    internal const string MemoryFactField = "Fact";

    /// <summary>The write form's tags field.</summary>
    internal const string MemoryTagsField = "Tags, comma separated";

    /// <summary>The write form's expiry field.</summary>
    internal const string MemoryExpiresField = "Expires on (yyyy-mm-dd, optional)";

    /// <summary>The write action.</summary>
    internal const string MemoryWrite = "Write fact";

    /// <summary>A write with no text.</summary>
    internal const string MemoryFactNeeded = "Type the fact first.";

    /// <summary>An expiry that is not a date.</summary>
    internal const string MemoryExpiresInvalid = "The expiry isn't a date. Use yyyy-mm-dd.";

    /// <summary>A write that stored a new fact.</summary>
    internal const string MemoryFactAdded = "Added.";

    /// <summary>A write that refreshed a fact already there.</summary>
    internal const string MemoryFactRefreshed = "Refreshed: that fact was already there.";

    /// <summary>A write that failed: the reason follows.</summary>
    internal static string MemoryWriteFailed(Error error) => "Couldn't write the fact. " + InstanceRefused(error);

    /// <summary>The forget action on a fact row.</summary>
    internal const string MemoryForgetFact = "Forget";

    /// <summary>The forget action for a tag: "Forget all tagged deploys".</summary>
    internal static string MemoryForgetTag(string tag) => "Forget all tagged " + tag;

    /// <summary>The forget action for every expired fact.</summary>
    internal const string MemoryForgetExpired = "Forget expired facts";

    /// <summary>A forget the API answered with nothing deleted.</summary>
    internal const string MemoryForgotNothing = "Nothing was forgotten.";

    /// <summary>A forget that failed: the reason follows. The rows stay.</summary>
    internal static string MemoryForgetFailed(Error error) => "Couldn't forget. " + InstanceRefused(error);

    /// <summary>A forget that worked: "Forgot 2 facts."</summary>
    internal static string MemoryForgot(int deleted) => "Forgot " + Facts(deleted) + ".";

    /// <summary>The line for a fact's refresh time.</summary>
    internal static string MemoryRefreshed(DateTimeOffset at) => "Refreshed " + at.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>The line for a fact's expiry.</summary>
    internal static string MemoryExpires(DateTimeOffset at, bool expired) =>
        (expired ? "Expired " : "Expires ") + at.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>The line for a fact's score in a search.</summary>
    internal static string MemoryScore(float score) => "Score " + score.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>Where a fact came from: "workflow w · run r".</summary>
    internal static string MemoryProvenanceLine(IEnumerable<(string Kind, string Value)> parts) =>
        string.Join(" · ", parts.Select(p => p.Kind + " " + p.Value));
}
