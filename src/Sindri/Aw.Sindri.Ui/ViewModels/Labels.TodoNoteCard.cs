using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): Settings' Obsidian todo note card and the Todo panel's sync line (todo spec §3.4, §4.5).</summary>
internal static partial class Labels
{
    /// <summary>The card's name.</summary>
    internal const string TodoCardName = "Obsidian todo note";

    /// <summary>The line under the card's name.</summary>
    internal const string TodoCardIntro = "Keep your todos in a note in an Obsidian vault. Sindri and the note change together.";

    /// <summary>The card when the library has no vault.</summary>
    internal const string TodoCardNoVaults = "Add an Obsidian vault in Settings → Library first";

    /// <summary>The button that brings the Library card into view.</summary>
    internal const string TodoCardOpenLibrary = "Go to Library";

    /// <summary>The vault box's label.</summary>
    internal const string TodoCardVault = "Vault";

    /// <summary>The vault box's name for a screen reader.</summary>
    internal const string TodoCardVaultName = "Obsidian vault";

    /// <summary>The note box's name for a screen reader.</summary>
    internal const string TodoCardNoteName = "Obsidian note, a path in the vault";

    /// <summary>The note box's name.</summary>
    internal const string TodoCardNote = "Note";

    /// <summary>The note box's hint.</summary>
    internal const string TodoCardNotePlaceholder = "Todo.md";

    /// <summary>The button that reads the note and writes nothing.</summary>
    internal const string TodoCardCheck = "Check note";

    /// <summary>The live line while the note is read.</summary>
    internal const string TodoCardChecking = "Checking the note…";

    /// <summary>The live line while the key is saved.</summary>
    internal const string TodoCardStarting = "Starting the sync…";

    /// <summary>The live line while the note is imported.</summary>
    internal const string TodoCardImporting = "Importing the note's tasks…";

    /// <summary>The live line while the key is removed.</summary>
    internal const string TodoCardStopping = "Stopping the sync…";

    /// <summary>The button that saves the key.</summary>
    internal const string TodoCardStart = "Start syncing";

    /// <summary>The button that saves the key when the disk can't swap files.</summary>
    internal const string TodoCardStartOneWay = "Start syncing (one way)";

    /// <summary>The button that saves the key, then imports.</summary>
    internal const string TodoCardImport = "Import and start syncing";

    /// <summary>The button that drops a check without saving anything.</summary>
    internal const string TodoCardCancel = "Cancel";

    /// <summary>The button that edits the vault and note of a running sync.</summary>
    internal const string TodoCardChange = "Change";

    /// <summary>The button that asks to stop the sync.</summary>
    internal const string TodoCardStop = "Stop syncing";

    /// <summary>The stop question's button that keeps the sync.</summary>
    internal const string TodoCardKeep = "Keep syncing";

    /// <summary>The disk can't swap files in place.</summary>
    /// <param name="note">The note's path.</param>
    /// <returns>The one-way words.</returns>
    internal static string TodoCardOneWay(string note) =>
        $"Sindri can't write to {NoteFile(note)} on this disk. It can bring in ticks and edits of lines that already have an id, and can't add new ones";

    /// <summary>The note is not there yet.</summary>
    /// <param name="note">The note's path.</param>
    /// <returns>"Todo.md doesn't exist yet. Sindri will create it.".</returns>
    internal static string TodoCardNewNote(string note) => $"{NoteFile(note)} doesn't exist yet. Sindri will create it.";

    /// <summary>The note has no open task.</summary>
    /// <param name="note">The note's path.</param>
    /// <returns>"Todo.md has no open tasks.".</returns>
    internal static string TodoCardNoTasks(string note) => $"{NoteFile(note)} has no open tasks.";

    /// <summary>What an import does.</summary>
    /// <param name="note">The note's path.</param>
    /// <param name="lines">The lines that get an id.</param>
    /// <param name="sindri">Sindri's own open todos, added after them.</param>
    /// <returns>"Import adds ids to 42 lines in Todo.md and adds your 6 Sindri todos after them".</returns>
    internal static string TodoCardCounts(string note, int lines, int sindri)
    {
        var head = string.Create(CultureInfo.InvariantCulture, $"Import adds ids to {lines} {(lines == 1 ? "line" : "lines")} in {NoteFile(note)}");
        return sindri == 0 ? head : string.Create(CultureInfo.InvariantCulture, $"{head} and adds your {sindri} Sindri {(sindri == 1 ? "todo" : "todos")} after them");
    }

    /// <summary>The tasks an import leaves as text.</summary>
    /// <param name="lines">Their 1-based line numbers, in note order.</param>
    /// <returns>"3 lines can't be imported (lines 4, 17, 30): they have a markdown link or a 📅 inside the text".</returns>
    internal static string TodoCardCantImport(IReadOnlyList<int> lines)
    {
        var shown = string.Join(", ", lines.Take(5).Select(n => n.ToString(CultureInfo.InvariantCulture)));
        var more = lines.Count > 5 ? string.Create(CultureInfo.InvariantCulture, $" and {lines.Count - 5} more: see `sindri doctor`") : string.Empty;
        return lines.Count == 1
            ? $"1 line can't be imported (line {shown}): it has a markdown link or a 📅 inside the text"
            : string.Create(CultureInfo.InvariantCulture, $"{lines.Count} lines can't be imported (lines {shown}{more}): they have a markdown link or a 📅 inside the text");
    }

    /// <summary>The cap cut the import.</summary>
    /// <param name="willImport">How many will import.</param>
    /// <param name="cap">The limit.</param>
    /// <returns>"Only 458 will import: 500 open todos is the limit".</returns>
    internal static string TodoCardOverCap(int willImport, int cap) => string.Create(CultureInfo.InvariantCulture, $"Only {willImport} will import: {cap} open todos is the limit");

    /// <summary>The note's open count changed between Check note and Import.</summary>
    /// <param name="note">The note's path.</param>
    /// <param name="open">The open tasks now.</param>
    /// <returns>"Todo.md changed: now 44 open tasks".</returns>
    internal static string TodoCardChanged(string note, int open) =>
        string.Create(CultureInfo.InvariantCulture, $"{NoteFile(note)} changed: now {open} open {(open == 1 ? "task" : "tasks")}");

    /// <summary>A set or stop was refused because the profile changed since the card last saw it; the card read it again.</summary>
    internal const string TodoCardProfileMoved = "Settings changed somewhere else, so Sindri read them again. Try again if it still looks right.";

    /// <summary>The settled card.</summary>
    /// <param name="vault">The vault's name.</param>
    /// <param name="note">The note's path.</param>
    /// <param name="lastSync">The last sync in the owner's time zone, or null.</param>
    /// <param name="state">The doctor row's words: "syncing", "one way", "waiting for import", "can't read", "note busy".</param>
    /// <returns>"Syncing with Vault/Todo.md · last synced 10:05".</returns>
    internal static string TodoCardSyncing(string vault, string note, DateTimeOffset? lastSync, string state)
    {
        var line = $"Syncing with {vault}/{note}";
        line += state switch
        {
            "one way" => " · one way",
            "waiting for import" => " · waiting for import",
            _ => string.Empty,
        };
        return lastSync is { } at ? line + " · last synced " + at.ToString("HH:mm", CultureInfo.InvariantCulture) : line;
    }

    /// <summary>The question before the key goes.</summary>
    /// <param name="note">The note's path.</param>
    /// <returns>"Stop syncing with Todo.md? The note stays as it is.".</returns>
    internal static string TodoCardStopAsk(string note) => $"Stop syncing with {NoteFile(note)}? The note stays as it is.";

    /// <summary>A refusal inside the card: the reason first, the key path (if the server named one) last, as served.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>The reason.</returns>
    internal static string TodoCardError(Kernel.Error error) => error.Message;

    private static string NoteFile(string note) => note[(note.LastIndexOf('/') + 1)..];

    /// <summary>The sync line when a note's state is "waiting for import".</summary>
    internal const string TodoSyncWaiting = "Waiting for import";

    /// <summary>The sync line when the disk can't swap files.</summary>
    internal const string TodoSyncOneWay = "Obsidian → Sindri only: can't write to this disk";

    /// <summary>The sync line when the note is busy.</summary>
    internal const string TodoSyncBusy = "Obsidian note busy, trying again";

    /// <summary>The sync line when a hold waits.</summary>
    /// <param name="count">The held changes.</param>
    /// <returns>"Paused: the note changed 14 todos".</returns>
    internal static string TodoSyncPaused(int count) => string.Create(CultureInfo.InvariantCulture, $"Paused: the note changed {count} {(count == 1 ? "todo" : "todos")}");

    /// <summary>The sync line when lines can't sync.</summary>
    /// <param name="lines">The lines, with their reasons.</param>
    /// <returns>"Line 14 can't sync: it has a markdown link", or "2 lines can't sync".</returns>
    internal static string TodoSyncCantSync(IReadOnlyList<Api.TodoCantSyncView> lines) =>
        lines.Count == 1 ? string.Create(CultureInfo.InvariantCulture, $"Line {lines[0].Line} can't sync: {lines[0].Reason}") : string.Create(CultureInfo.InvariantCulture, $"{lines.Count} lines can't sync");

    /// <summary>The sync line when the note can't be read.</summary>
    /// <param name="reason">The failure's reason, or null.</param>
    /// <returns>"Obsidian note: can't read, Todo.md is missing".</returns>
    internal static string TodoSyncCantRead(string? reason) => string.IsNullOrEmpty(reason) ? "Obsidian note: can't read" : $"Obsidian note: can't read, {reason}";

    /// <summary>The sync line while a pick's Undo window runs.</summary>
    /// <param name="seconds">The seconds left.</param>
    /// <returns>"Saving your pick to Obsidian in 6 s".</returns>
    internal static string TodoSyncSavingPick(int seconds) => string.Create(CultureInfo.InvariantCulture, $"Saving your pick to Obsidian in {seconds} s");

    /// <summary>The sync line when all is well.</summary>
    /// <param name="lastSync">The last sync in the owner's time zone, or null.</param>
    /// <returns>"Synced with Obsidian 10:05".</returns>
    internal static string TodoSyncSynced(DateTimeOffset? lastSync) => lastSync is { } at ? "Synced with Obsidian " + at.ToString("HH:mm", CultureInfo.InvariantCulture) : "Synced with Obsidian";
}
