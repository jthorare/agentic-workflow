using System.Globalization;
using Aw.Kernel;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): Settings' Library card, the folders the design library reads (review page spec §6.3, §16).</summary>
internal static partial class Labels
{
    /// <summary>The card's heading.</summary>
    internal const string LibraryFoldersHeading = "Library";

    /// <summary>The line under the heading.</summary>
    internal const string LibraryFoldersIntro = "Folders Sindri reads design docs from. Only files whose front matter says aw-doc: 1 are read; notes, journals and secrets are skipped.";

    /// <summary>Before the first answer.</summary>
    internal const string LibraryFoldersReading = "Reading the folder…";

    /// <summary>No folders at all (an agent session gets none).</summary>
    internal const string LibraryFoldersEmpty = "No folders yet. Add a folder to index its design docs.";

    /// <summary>No folders and nothing to add (an agent session's).</summary>
    internal const string LibraryFoldersNone = "No folders yet.";

    /// <summary>The button that opens the form.</summary>
    internal const string LibraryFoldersAdd = "Add folder";

    /// <summary>The form's heading.</summary>
    internal const string LibraryFoldersFormTitle = "Add a folder";

    /// <summary>The name field's label.</summary>
    internal const string LibraryFoldersNameLabel = "Name";

    /// <summary>The folder field's label.</summary>
    internal const string LibraryFoldersPathLabel = "Folder";

    /// <summary>The deny field's label.</summary>
    internal const string LibraryFoldersDenyLabel = "Don't read";

    /// <summary>The deny field's example.</summary>
    internal const string LibraryFoldersDenyPlaceholder = "Private/**, Journal/**";

    /// <summary>The chooser button.</summary>
    internal const string LibraryFoldersBrowse = "Browse";

    /// <summary>The chooser's title.</summary>
    internal const string LibraryFoldersBrowseTitle = "Choose the folder to read design docs from";

    /// <summary>Under the form's fields.</summary>
    internal const string LibraryFoldersFormNote = "Name: lowercase letters, digits and \"-\". The change asks for your approval; as the only waiting change it approves in one click. Docs naming one synced repo are visible to that repo's agent sessions.";

    /// <summary>The form's save button.</summary>
    internal const string LibraryFoldersSave = "Add and approve";

    /// <summary>The save button while the call is on its way.</summary>
    internal const string LibraryFoldersSaving = "Saving…";

    /// <summary>The remove button of a vault.</summary>
    internal const string LibraryFoldersRemove = "Remove";

    /// <summary>The turn-off button of the aw folder.</summary>
    internal const string LibraryFoldersTurnOff = "Turn off";

    /// <summary>The turn-on button of the aw folder while it is off.</summary>
    internal const string LibraryFoldersTurnOn = "Turn on";

    /// <summary>What a repo's folder says in place of a button.</summary>
    internal const string LibraryFoldersReadOnly = "set by the repo list";

    /// <summary>The aw folder's chip.</summary>
    internal const string LibraryFoldersBuiltIn = "built in";

    /// <summary>A vault's chip.</summary>
    internal const string LibraryFoldersVault = "vault";

    /// <summary>A repo folder's chip.</summary>
    internal const string LibraryFoldersFromRepos = "from your repos";

    /// <summary>The aw folder while it is turned off.</summary>
    internal const string LibraryFoldersOff = "turned off";

    /// <summary>The line beside each vault: who can read its docs.</summary>
    internal const string LibraryFoldersVaultNote = "Docs naming one synced repo are visible to that repo's agent sessions.";

    /// <summary>The errors chip of a folder.</summary>
    /// <param name="errors">How many docs have an error.</param>
    /// <returns>"1 error" or "2 errors".</returns>
    internal static string LibraryFoldersErrors(int errors) => string.Create(CultureInfo.InvariantCulture, $"{errors} {(errors == 1 ? "error" : "errors")}");

    /// <summary>The errors chip's screen-reader name.</summary>
    /// <param name="name">The folder's name.</param>
    /// <param name="errors">How many docs have an error.</param>
    /// <returns>"2 errors in work".</returns>
    internal static string LibraryFoldersErrorsName(string name, int errors) => LibraryFoldersErrors(errors) + " in " + name;

    /// <summary>The remove confirmation's question.</summary>
    /// <param name="name">The vault's name.</param>
    /// <returns>The words.</returns>
    internal static string LibraryFoldersRemoveAsk(string name) => $"Remove {name}? Sindri stops reading it; the folder itself is left alone.";

    /// <summary>The remove button's screen-reader name.</summary>
    /// <param name="name">The vault's name.</param>
    /// <returns>"Remove work".</returns>
    internal static string LibraryFoldersRemoveName(string name) => "Remove " + name;

    /// <summary>The turn-off or turn-on button's screen-reader name.</summary>
    /// <param name="on">True for the turn-on button.</param>
    /// <returns>"Turn off aw" or "Turn on aw".</returns>
    internal static string LibraryFoldersAwName(bool on) => (on ? LibraryFoldersTurnOn : LibraryFoldersTurnOff) + " aw";

    /// <summary>A folder that was read.</summary>
    /// <param name="age">How long ago it was read.</param>
    /// <param name="docs">The docs read.</param>
    /// <param name="errors">The docs with an error.</param>
    /// <returns>"scanned 3 min ago, 9 docs, 0 errors".</returns>
    internal static string LibraryFoldersScanned(TimeSpan age, int docs, int errors) =>
        string.Create(CultureInfo.InvariantCulture, $"scanned {LibraryFoldersAgo(age)}, {docs} {(docs == 1 ? "doc" : "docs")}, {LibraryFoldersErrors(errors)}");

    /// <summary>A folder that can't be read.</summary>
    /// <param name="reason">Sindri's reason.</param>
    /// <returns>"can't be read: permission denied".</returns>
    internal static string LibraryFoldersUnreadable(string reason) => "can't be read: " + reason;

    /// <summary>A change the API refused.</summary>
    /// <param name="error">The refusal.</param>
    /// <returns>The words, with the code and detail.</returns>
    internal static string LibraryFoldersFailed(Error error) => "Couldn't change the library. " + InstanceRefused(error);

    /// <summary>The read of the folders failed.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>The words, with the code and detail.</returns>
    internal static string LibraryFoldersReadFailed(Error error) => "Couldn't read the library folders just now. " + InstanceRefused(error);

    private static string LibraryFoldersAgo(TimeSpan age) => age.TotalMinutes switch
    {
        < 1 => "just now",
        < 60 => string.Create(CultureInfo.InvariantCulture, $"{(int)age.TotalMinutes} min ago"),
        < 1440 => string.Create(CultureInfo.InvariantCulture, $"{(int)age.TotalHours} h ago"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{(int)age.TotalDays} d ago"),
    };
}
