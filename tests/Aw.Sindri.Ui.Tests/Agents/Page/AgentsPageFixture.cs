using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Json;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Agents;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Agents.Page;

/// <summary>What the Agents page tests share: the API's goldens as the served list, a clock at the golden's asOf, and edits of a golden as JSON.</summary>
internal sealed class AgentsPageFixture
{
    internal static readonly Uri Url = new("http://127.0.0.1:7410/");

    internal static readonly DateTimeOffset AsOf = DateTimeOffset.Parse("2026-10-16T12:05:00Z", System.Globalization.CultureInfo.InvariantCulture);

    internal FakeSindriApi Server { get; } = new();

    internal FakeClipboard Clipboard { get; } = new();

    internal FakeTimeProvider Clock { get; } = new(AsOf);

    internal CatalogViewModel Catalog { get; }

    internal AgentsPageFixture(string? focusUi = null)
    {
        Catalog = new CatalogViewModel(Clipboard, Clock);
        var catalog = JsonNode.Parse(GoldenFiles.Api("commands", "catalog"))!; // why: the golden is a JSON document
        if (focusUi is not null)
        {
            catalog["rows"]!.AsArray().Single(r => (string?)r!["action"] == "FocusSession")!["ui"] = focusUi; // why: the golden has the row
        }

        Catalog.Show(Url, Server, AwJson.Deserialize<CommandList>(System.Text.Encoding.UTF8.GetBytes(catalog.ToJsonString()), SindriApiClient.Json).Value);
    }

    internal static JsonNode Golden(string slug, string scenario) => JsonNode.Parse(GoldenFiles.Api(slug, scenario))!; // why: the golden is a JSON document

    /// <summary>Serves this list from now on.</summary>
    internal void Serve(JsonNode list) => Server.SessionAnswer = new RouteAnswer(HttpStatusCode.OK, list.ToJsonString());

    internal void Serve(string scenario) => Server.SessionAnswer = RouteAnswer.Api("sessions", scenario);

    /// <summary>The three-provider golden, edited.</summary>
    internal static JsonNode Edited(Action<JsonObject> edit)
    {
        var list = Golden("sessions", "fixture-three-providers");
        edit(list.AsObject());
        return list;
    }

    /// <summary>The golden's own row for this provider prefix: rows a test adds are appended, so the first match stays the golden's.</summary>
    internal static JsonObject Row(JsonNode list, string prefix) =>
        list["sessions"]!.AsArray().Select(r => r!.AsObject()).First(r => ((string)r["ref"]!).StartsWith(prefix, StringComparison.Ordinal)); // why: every row has a ref; First, because a test that adds a row with the same prefix appends it after the golden's own

    internal static void Remove(JsonNode list, string prefix)
    {
        var rows = list["sessions"]!.AsArray();
        _ = rows.Remove(Row(list, prefix));
    }

    internal AgentsPageViewModel Page() => new(Server, Clipboard, Clock, Catalog);

    /// <summary>A page that is shown and has read the list once.</summary>
    internal async Task<AgentsPageViewModel> Shown(CancellationToken token)
    {
        var page = Page();
        page.SetShown(shown: true);
        await page.Refresh(Url, token);
        return page;
    }
}
