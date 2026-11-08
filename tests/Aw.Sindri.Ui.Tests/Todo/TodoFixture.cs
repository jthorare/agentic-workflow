using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Todo;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Todo;

/// <summary>What the Todo panel tests share: the API's list golden as the served list, a clock at 2026-01-01 (the golden's "now", Thursday), and edits of the golden as JSON.</summary>
internal sealed class TodoFixture
{
    internal static readonly Uri Url = new("http://127.0.0.1:7410/");

    internal FakeSindriApi Server { get; } = new();

    internal FakeTimeProvider Clock { get; } = new(DateTimeOffset.Parse("2026-01-01T00:00:00Z", CultureInfo.InvariantCulture));

    internal static JsonNode Golden() => JsonNode.Parse(GoldenFiles.Api("todos", "list"))!; // why: the golden is a JSON document

    internal void Serve(JsonNode list) => Server.TodosAnswer = new RouteAnswer(HttpStatusCode.OK, list.ToJsonString());

    /// <summary>Serves the golden with these todos instead of its own.</summary>
    internal void ServeTodos(params JsonNode[] todos)
    {
        var list = Golden();
        list["todos"] = new JsonArray(todos);
        Serve(list);
    }

    /// <summary>An open todo with no links, added and last changed by the owner.</summary>
    internal static JsonNode Open(string id, string text, string? due = null)
    {
        var todo = Golden()["todos"]![3]!.DeepClone();   // "call the dentist": open, no date, no links
        todo["id"] = id;
        todo["text"] = text;
        todo["due"] = due;
        todo["addedBy"] = "human:ui";
        todo["lastBy"] = "human:ui";
        return todo;
    }

    /// <summary>One golden todo by its position in the file, cloned.</summary>
    internal static JsonNode Of(int index) => Golden()["todos"]![index]!.DeepClone();

    internal TodoPanelViewModel Panel() => new(Server, Clock);

    /// <summary>A panel that has read the list once.</summary>
    internal async Task<TodoPanelViewModel> Read(CancellationToken token)
    {
        var panel = Panel();
        await panel.Refresh(Url, token);
        return panel;
    }

    /// <summary>A panel that has read the list once and is expanded.</summary>
    internal async Task<TodoPanelViewModel> Expanded(CancellationToken token)
    {
        var panel = await Read(token);
        panel.Expand();
        return panel;
    }
}
