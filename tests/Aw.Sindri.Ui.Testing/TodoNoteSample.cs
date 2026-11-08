using System.Text.Json.Nodes;

namespace Aw.Sindri.Ui.Testing;

/// <summary>
/// The app's own samples of GET /sindri/todos/note, in the shape of the API's hand-written note.json and hold.json: a note that is syncing with nothing to sort out (the default the
/// fake serves, so every panel test sees no attention rows), the same with conflicts, and the same with an open hold.
/// </summary>
internal static class TodoNoteSample
{
    /// <summary>A note that syncs, with no conflict, no hold, no pick and no line that can't sync.</summary>
    internal static string Quiet => Note([], hold: null);

    /// <summary>A syncing note with these todos in conflict, oldest first.</summary>
    /// <param name="ids">The conflicted todos' ids.</param>
    /// <returns>The note as JSON.</returns>
    internal static string WithConflicts(params string[] ids) => Note(ids, hold: null);

    /// <summary>A syncing note with an open hold of <paramref name="count"/> changes (a removal, an edit, a reopened todo, then more removals) and these conflicts.</summary>
    /// <param name="count">How many changes the hold holds.</param>
    /// <param name="noteHash">The hold's noteHash.</param>
    /// <param name="conflicts">The conflicted todos' ids, oldest first.</param>
    /// <returns>The note as JSON.</returns>
    internal static string WithHold(int count, string noteHash = "sha256:3f1c", params string[] conflicts)
    {
        var changes = new JsonArray();
        for (var i = 0; i < count; i++)
        {
            changes.Add(Change(i));
        }

        return Note(conflicts, new JsonObject { ["noteHash"] = noteHash, ["heldAt"] = "2026-01-01T10:04:41.0000000Z", ["openInBase"] = count * 3, ["changes"] = changes });
    }

    private static JsonObject Change(int i)
    {
        var n = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return i switch
        {
            0 => new JsonObject { ["id"] = "td_hold_0", ["kind"] = "removed", ["text"] = "reply to Sam about the offsite", ["noteLine"] = null },
            1 => new JsonObject { ["id"] = "td_hold_1", ["kind"] = "edited", ["text"] = "call the dentist", ["noteLine"] = "- [ ] call the dentist about the crown 📅 2026-01-05 ^td-hold-1" },
            2 => new JsonObject { ["id"] = "td_hold_2", ["kind"] = "reopened", ["text"] = "renew the cert", ["noteLine"] = "- [ ] renew the cert 📅 2025-12-30 ^td-hold-2" },
            _ => new JsonObject { ["id"] = "td_hold_" + n, ["kind"] = "removed", ["text"] = "held todo " + n, ["noteLine"] = null },
        };
    }

    private static string Note(string[] conflicts, JsonObject? hold) =>
        new JsonObject
        {
            ["asOf"] = new JsonObject { ["cursor"] = 14, ["ledgerHead"] = 14, ["projectedAt"] = "2026-01-01T00:00:00.0000000Z", ["rebuilding"] = false, ["stalled"] = false },
            ["state"] = "syncing",
            ["vault"] = "notes",
            ["note"] = "Todo.md",
            ["lastCheckAt"] = "2026-01-01T09:59:30.0000000Z",
            ["lastSyncAt"] = "2026-01-01T09:58:12.0000000Z",
            ["lastError"] = null,
            ["conflicts"] = new JsonArray([.. conflicts.Select(c => JsonValue.Create(c))]),
            ["hold"] = hold,
            ["picks"] = new JsonArray(),
            ["cantSync"] = new JsonArray(),
        }.ToJsonString();
}
