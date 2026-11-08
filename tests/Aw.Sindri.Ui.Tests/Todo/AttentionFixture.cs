using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Todo;
using static Aw.Sindri.Ui.Tests.Todo.TodoFixture;

namespace Aw.Sindri.Ui.Tests.Todo;

/// <summary>What the attention-row tests share: todos with an open conflict, a note that names them, and a panel that records the picks' toasts and the focus asks.</summary>
internal sealed class AttentionFixture
{
    internal TodoFixture F { get; } = new();

    internal List<TodoPickedEventArgs> Picked { get; } = [];

    internal int ButtonsAsked { get; set; }

    /// <summary>One version of a conflict.</summary>
    internal static JsonNode Side(string? text, string? due = "2026-01-02", bool done = false, bool removed = false, params string[] links) =>
        new JsonObject { ["text"] = text, ["due"] = due, ["links"] = new JsonArray([.. links.Select(l => JsonValue.Create(l))]), ["done"] = done, ["removed"] = removed };

    /// <summary>An open todo with an open both-places conflict.</summary>
    internal static JsonNode Conflicted(string id, string text, JsonNode sindri, JsonNode obsidian, string noteHash = "sha256:ab")
    {
        var todo = Open(id, text, "2026-01-02");
        todo["conflict"] = new JsonObject { ["sindri"] = sindri.DeepClone(), ["obsidian"] = obsidian.DeepClone(), ["noteHash"] = noteHash };
        return todo;
    }

    /// <summary>A conflict where only the text differs.</summary>
    internal static JsonNode TextOnly(string id, string sindri = "reply to Sam", string obsidian = "reply to Sam today") =>
        Conflicted(id, sindri, Side(sindri), Side(obsidian));

    /// <summary>Six open todos with no conflict, after the conflicted ones.</summary>
    internal static JsonNode[] Six() => [.. Enumerable.Range(0, 6).Select(i => Open("td_open" + i.ToString(CultureInfo.InvariantCulture), "open " + i.ToString(CultureInfo.InvariantCulture)))];

    /// <summary>Serves the todos and the note.</summary>
    internal void Serve(string note, params JsonNode[] todos)
    {
        F.Server.TodoNoteAnswer = new RouteAnswer(HttpStatusCode.OK, note);
        F.ServeTodos(todos);
    }

    /// <summary>A panel that has read the todos and the note once.</summary>
    internal async Task<TodoPanelViewModel> Read(CancellationToken token)
    {
        var panel = await F.Read(token);
        panel.Picked += (_, e) => Picked.Add(e);
        panel.AttentionButtonsRequested += (_, _) => ButtonsAsked++;
        return panel;
    }
}
