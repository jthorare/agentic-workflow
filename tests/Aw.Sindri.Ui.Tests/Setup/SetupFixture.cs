using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Json;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Setup;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Setup;

/// <summary>
/// What the Setup tests share: the setup goldens as the served list (spec-shaped copies of api #458's own, PROVISIONAL until it merges), the catalog as served, a fake clock and the
/// opener the shell would give the card. A step the tests need and no golden has is written as JSON here.
/// </summary>
internal sealed class SetupFixture
{
    internal static readonly Uri Url = new("http://127.0.0.1:7410/");

    internal FakeSindriApi Server { get; } = new();

    internal FakeClipboard Clipboard { get; } = new();

    internal FakeFolderPicker Folders { get; } = new();

    internal FakeTimeProvider Clock { get; } = new(A.Noon);

    internal CatalogViewModel Catalog { get; }

    /// <summary>The actions the card opens in the app, as the shell decides it; none by default.</summary>
    internal HashSet<string> InApp { get; } = new(StringComparer.Ordinal);

    /// <param name="catalog">The catalog scenario.</param>
    /// <param name="cliOnly">Actions the served catalog lists as cli-only (Sindri does not let the app do them): each is the command to copy.</param>
    internal SetupFixture(string catalog = "catalog", params string[] cliOnly)
    {
        Catalog = new CatalogViewModel(Clipboard, Clock);
        var json = CatalogJson(catalog, cliOnly);
        Server.Catalog = new RouteAnswer(HttpStatusCode.OK, json);
        Catalog.Show(Url, Server, AwJson.Deserialize<CommandList>(System.Text.Encoding.UTF8.GetBytes(json), SindriApiClient.Json).Value);
    }

    /// <summary>The served catalog of a scenario ("extra-" ones are the app's own, SpecShaped, for a catalog the API has no golden for), with some actions listed as cli-only.</summary>
    /// <param name="catalog">The catalog scenario.</param>
    /// <param name="cliOnly">The actions to list as cli-only.</param>
    /// <returns>The catalog as JSON.</returns>
    internal static string CatalogJson(string catalog, params string[] cliOnly)
    {
        var node = JsonNode.Parse(IsExtra(catalog) ? GoldenFiles.SpecShaped("commands", catalog) : GoldenFiles.Api("commands", catalog))!;   // why: the golden is a JSON document
        foreach (var row in node["rows"]!.AsArray().Where(r => cliOnly.Contains((string?)r!["action"], StringComparer.Ordinal)))   // why: the catalog has rows
        {
            row!["ui"] = "cli-only:local";   // why: a row of the catalog
        }

        return node.ToJsonString();
    }

    /// <summary>The route answer for <see cref="CatalogJson"/>.</summary>
    /// <param name="catalog">The catalog scenario.</param>
    /// <param name="cliOnly">The actions to list as cli-only.</param>
    /// <returns>The answer.</returns>
    internal static RouteAnswer CatalogAnswer(string catalog, params string[] cliOnly) => new(HttpStatusCode.OK, CatalogJson(catalog, cliOnly));

    internal void Serve(string scenario) => Server.SetupAnswer = Answer(scenario);

    internal void Serve(JsonNode list) => Server.SetupAnswer = new RouteAnswer(HttpStatusCode.OK, list.ToJsonString());

    /// <summary>The route answer for a setup scenario: Sindri'"'"'s own golden, or for an "extra-" one the app'"'"'s own (a shape the API has no golden for, such as three running steps and a failed Onyx).</summary>
    internal static RouteAnswer Answer(string scenario) => IsExtra(scenario) ? RouteAnswer.SpecShaped("setup", scenario) : RouteAnswer.Api("setup", scenario);

    internal static JsonNode Golden(string scenario) => JsonNode.Parse(IsExtra(scenario) ? GoldenFiles.SpecShaped("setup", scenario) : GoldenFiles.Api("setup", scenario))!; // why: the golden is a JSON document

    private static bool IsExtra(string scenario) => scenario.StartsWith("extra-", StringComparison.Ordinal);

    /// <summary>The golden with one step changed: its state and, when given, its action.</summary>
    internal static JsonNode With(string scenario, string step, string state, string? action = null) => With(Golden(scenario), step, state, action);

    /// <summary>The list with one step changed: its state and, when given, its action.</summary>
    internal static JsonNode With(JsonNode list, string step, string state, string? action = null)
    {
        var served = list["steps"]!.AsArray().Single(s => (string)s!["id"]! == step)!;
        served["state"] = state;
        served["action"] = action;
        return list;
    }

    /// <summary>The golden with one step's items replaced by (repo, state, detail) lines.</summary>
    internal static JsonNode WithItems(JsonNode list, string step, params (string Repo, string State, string? Detail)[] items)
    {
        var served = list["steps"]!.AsArray().Single(s => (string)s!["id"]! == step)!;
        served["items"] = new JsonArray([.. items.Select(i => (JsonNode)new JsonObject { ["repo"] = i.Repo, ["state"] = i.State, ["detail"] = i.Detail })]);
        return list;
    }

    /// <summary>The golden with one repo line's providers set, as (provider, state, detail) the API serves them; the line keeps its other fields.</summary>
    internal static JsonNode WithProviders(JsonNode list, string step, string repo, params (string Provider, string State, string? Detail)[] providers)
    {
        var line = list["steps"]!.AsArray().Single(s => (string)s!["id"]! == step)!["items"]!.AsArray().Single(i => (string)i!["repo"]! == repo)!;
        line["providers"] = new JsonArray([.. providers.Select(p => (JsonNode)new JsonObject { ["provider"] = p.Provider, ["state"] = p.State, ["detail"] = p.Detail })]);
        return list;
    }

    /// <summary>The golden with one step's error and progress set (a failed step that also counts).</summary>
    internal static JsonNode WithProgress(JsonNode list, string step, int done, int total, string? unit = null)
    {
        var served = list["steps"]!.AsArray().Single(s => (string)s!["id"]! == step)!;
        served["progress"] = new JsonObject { ["done"] = done, ["total"] = total, ["unit"] = unit };
        return list;
    }

    /// <summary>The live profile's hash the typed approval is tied to; none until a test sets it.</summary>
    internal string? Hash { get; set; }

    internal SetupViewModel Setup() => new(Server, Catalog, Clock, InApp.Contains, Folders, () => Hash);

    /// <summary>A view model that has read the list once.</summary>
    internal async Task<SetupViewModel> Read(CancellationToken token)
    {
        var setup = Setup();
        await setup.Refresh(Url, token);
        return setup;
    }
}
