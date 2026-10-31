using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Pin sheet (concept pins spec §3.6, §5). Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>The sheet's title for Pin this.</summary>
    internal const string PinSheetHeadingPin = "Pin this";

    /// <summary>The sheet's title for Edit pin.</summary>
    internal const string PinSheetHeadingEdit = "Edit pin";

    /// <summary>The sheet's title for New pin.</summary>
    internal const string PinSheetHeadingNew = "New pin";

    /// <summary>The name field.</summary>
    internal const string PinSheetName = "Name";

    /// <summary>The definition field.</summary>
    internal const string PinSheetDefinition = "Definition";

    /// <summary>The line under the definition.</summary>
    internal const string PinSheetDefinitionHint = "Shown to people and agents; it moves no records and needs no rebuild.";

    /// <summary>The words field.</summary>
    internal const string PinSheetWords = "The words people use";

    /// <summary>The box a word is typed in.</summary>
    internal const string PinSheetAddWordBox = "Add a word";

    /// <summary>The button beside it.</summary>
    internal const string PinSheetAddWord = "Add word";

    /// <summary>The features field.</summary>
    internal const string PinSheetFeatures = "Features";

    /// <summary>The picker's search box.</summary>
    internal const string PinSheetFindFeature = "Find a feature";

    /// <summary>The picker's button on a result.</summary>
    internal const string PinSheetAddFeature = "Add";

    /// <summary>The picker while it reads.</summary>
    internal const string PinSheetFeaturesLoading = "Loading features…";

    /// <summary>The picker when its read failed.</summary>
    internal const string PinSheetFeaturesFailed = "Couldn't read the map's features just now. Try again.";

    /// <summary>The picker's retry button.</summary>
    internal const string PinSheetTryAgain = "Try again";

    /// <summary>The picker when the map has no features.</summary>
    internal const string PinSheetNoFeatures = "The map has no features yet. Build it first.";

    /// <summary>The picker when a search finds none.</summary>
    internal static string PinSheetNoFeatureMatches(string search) => "No feature matches \"" + search + "\".";

    /// <summary>A word whose records are still counted.</summary>
    internal const string PinSheetCounting = "Counting records…";

    /// <summary>The words when the terms couldn't be read.</summary>
    internal const string PinSheetTermsUnreadable = "Couldn't read this concept's words just now. Type them, or try again later.";

    /// <summary>"about 41 more records".</summary>
    internal static string PinSheetMoreRecords(int count) =>
        string.Create(CultureInfo.InvariantCulture, $"about {count:N0} more {(count == 1 ? "record" : "records")}");

    /// <summary>A word that would link too many records.</summary>
    internal static string PinSheetTooBroad(int count) => string.Create(CultureInfo.InvariantCulture, $"About {count:N0} more records, likely too broad");

    /// <summary>A word another pin already owns.</summary>
    internal static string PinSheetWordTaken(string pin, string word) => "Already a word of the pin " + pin + " (\"" + word + "\")";

    /// <summary>A word that is no alias.</summary>
    internal const string PinSheetWordShape = "A word is 1 to 60 characters with a letter or digit, and not only small words like \"the\".";

    /// <summary>The note on Edit pin when the name changes.</summary>
    internal const string PinSheetRename = "Renaming rebuilds the map (about 4 minutes) before the card shows the new name.";

    /// <summary>The note under the button when nothing would match.</summary>
    internal const string PinSheetEmpty = "Nothing in the map will match this pin yet; you can still save it.";

    /// <summary>The read-only keys beside Edit pin.</summary>
    internal static string PinSheetFolderRules(int count) =>
        string.Create(CultureInfo.InvariantCulture, $"{count} folder {(count == 1 ? "rule" : "rules")} (paths, dependsOn, owns). Set outside the app; kept as is.");

    /// <summary>The submit button at rest.</summary>
    internal static string PinSheetSubmit(bool edit, bool approve) => (edit ? "Save" : "Pin") + (approve ? " and approve" : string.Empty);

    /// <summary>The submit button while the write is in flight.</summary>
    internal static string PinSheetSubmitting(bool edit) => edit ? "Saving…" : "Pinning…";

    /// <summary>The Unpin button that asks.</summary>
    internal const string PinSheetUnpin = "Unpin";

    /// <summary>The Unpin question's confirm button at rest.</summary>
    internal static string PinSheetUnpinConfirm(bool approve) => approve ? "Unpin and approve" : "Unpin";

    /// <summary>The Unpin question's confirm button while the write is in flight.</summary>
    internal const string PinSheetUnpinning = "Unpinning…";

    /// <summary>Answers the Unpin question with no.</summary>
    internal const string PinSheetKeepPin = "Keep pin";

    /// <summary>The Unpin question.</summary>
    internal static string PinSheetUnpinQuestion(string name, bool neverApproved, int folderRules)
    {
        var text = neverApproved
            ? "Drop the pin " + name + "? It was never approved, so nothing in the map changes."
            : "Unpin " + name + "? It comes back as a suggestion once the map rebuilds.";
        return folderRules > 0
            ? text + string.Create(CultureInfo.InvariantCulture, $" It also has {folderRules} folder {(folderRules == 1 ? "rule" : "rules")} set outside the app; Unpin removes {(folderRules == 1 ? "it" : "them")} too.")
            : text;
    }

    /// <summary>The question before a discard.</summary>
    internal const string PinSheetDiscardQuestion = "Discard your changes?";

    /// <summary>Discards.</summary>
    internal const string PinSheetDiscard = "Discard";

    /// <summary>Goes back to the fields.</summary>
    internal const string PinSheetKeepEditing = "Keep editing";

    /// <summary>The close button.</summary>
    internal const string PinSheetEsc = "Esc";

    /// <summary>Under the button: the profile moved while the sheet was open.</summary>
    internal static string PinSheetClash(string button) => "The settings changed while you were editing. Your changes are still here. Press " + button + " again.";

    /// <summary>Under the button: the write ran past the wait.</summary>
    internal const string PinSheetTimedOut = "The pin write is still running. Check Jobs, then try again.";

    /// <summary>Under the button: the write failed without a sentence of the server's.</summary>
    internal const string PinSheetFailed = "Couldn't write the pin just now. Try again.";

    /// <summary>Under the button: the name is empty.</summary>
    internal const string PinSheetNameRequired = "Give the pin a name.";

    /// <summary>Under the button: the name or definition is too long.</summary>
    internal const string PinSheetTooLong = "The name takes up to 80 characters and the definition up to 280.";
}
