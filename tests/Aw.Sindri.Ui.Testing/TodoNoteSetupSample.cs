using System.Text.Json.Nodes;

namespace Aw.Sindri.Ui.Testing;

/// <summary>
/// The app's own samples of the todo note setup answers (check, import, set, stop). Guessed shapes: todo spec §4.4 lists what the check holds and no golden exists yet; Ph3b's goldens replace these when they land.
/// </summary>
internal static class TodoNoteSetupSample
{
    /// <summary>A check of a note that is not there yet.</summary>
    internal static string NewNote => Check(exists: false, open: 0);

    /// <summary>A check of a note.</summary>
    /// <param name="exists">False for a new note.</param>
    /// <param name="open">The open tasks.</param>
    /// <param name="cantImport">The 1-based line numbers of open tasks that can't be imported.</param>
    /// <param name="willImport">How many an import adopts; null for every importable one.</param>
    /// <param name="cap">The open todo limit.</param>
    /// <param name="sindriOpen">Sindri's own open todos.</param>
    /// <param name="canSwap">False when the disk can't swap files.</param>
    /// <returns>The check as JSON.</returns>
    internal static string Check(bool exists = true, int open = 42, int[]? cantImport = null, int? willImport = null, int cap = 500, int sindriOpen = 6, bool canSwap = true) =>
        new JsonObject
        {
            ["exists"] = exists,
            ["open"] = open,
            ["done"] = exists ? 9 : 0,
            ["other"] = exists ? 3 : 0,
            ["cantImport"] = new JsonArray([.. (cantImport ?? []).Select(line => (JsonNode)new JsonObject { ["line"] = line, ["reason"] = "it has a markdown link" })]),
            ["willImport"] = willImport ?? (open - (cantImport?.Length ?? 0)),
            ["cap"] = cap,
            ["sindriOpen"] = sindriOpen,
            ["canSwap"] = canSwap,
        }.ToJsonString();

    /// <summary>The import's answer.</summary>
    internal static string Imported => """{"imported":39,"cantImport":3}""";

    /// <summary>The set's and the stop's answer when the profile changed.</summary>
    internal static string Changed => """{"changed":true}""";

    /// <summary>A note read in this state, with the vault "work" and the note "Todo.md".</summary>
    /// <param name="state">The doctor row's words.</param>
    /// <param name="lastError">The last failure's reason, or null.</param>
    /// <param name="lastSyncAt">The last sync, or null.</param>
    /// <param name="cantSync">Lines that can't sync: line and reason.</param>
    /// <param name="hold">The held changes' count, or 0 for no hold.</param>
    /// <param name="pickUndoUntil">The end of a pick's Undo window, or null for no pick.</param>
    /// <returns>The note as JSON.</returns>
    internal static string NoteIn(string state, string? lastError = null, string? lastSyncAt = "2026-01-01T09:58:12.0000000Z", (int Line, string Reason)[]? cantSync = null, int hold = 0, string? pickUndoUntil = null)
    {
        var note = JsonNode.Parse(TodoNoteSample.Quiet)!;   // why: the sample is a JSON document
        var set = !string.Equals(state, "not set", StringComparison.Ordinal);
        note["state"] = state;
        note["vault"] = set ? "work" : null;
        note["note"] = set ? "Todo.md" : null;
        note["lastError"] = lastError;
        note["lastSyncAt"] = lastSyncAt;
        note["cantSync"] = new JsonArray([.. (cantSync ?? []).Select(c => (JsonNode)new JsonObject { ["line"] = c.Line, ["reason"] = c.Reason })]);
        if (hold > 0)
        {
            note["hold"] = JsonNode.Parse(TodoNoteSample.WithHold(hold))!["hold"]!.DeepClone();
        }

        if (pickUndoUntil is not null)
        {
            note["picks"] = new JsonArray(new JsonObject { ["kind"] = "conflict", ["id"] = "td_c1", ["kept"] = "obsidian", ["by"] = "human:ui", ["undoUntil"] = pickUndoUntil });
        }

        return note.ToJsonString();
    }
}
