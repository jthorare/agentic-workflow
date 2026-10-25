using System.Net;
using System.Text.Json.Nodes;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Start;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Start;

/// <summary>The Start form over the fake API: recipes and inputs are the API's goldens, bent only where a case needs a field the goldens have not got (named at the call).</summary>
internal sealed class StartFormFixture
{
    internal static readonly Uri Api = new("http://127.0.0.1:7410/");

    // Recipes whose goldens carry the inputs a case needs: text, item, payload and file.
    internal const string Step = "workflow.step";
    internal const string Triage = "workflow.dev-triage-three-state";
    internal const string Project = "workflow.project";
    internal const string DeskRoundTrip = "workflow.dev-desk-round-trip";

    internal StartFormFixture()
    {
        Server = new FakeSindriApi { Catalog = RouteAnswer.Api("commands", "catalog") };
        Files = new FakeFilePicker();
    }

    internal FakeSindriApi Server { get; }

    internal FakeFilePicker Files { get; }

    internal FakeTimeProvider Clock { get; } = new(A.Noon);

    internal static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A form over the catalog as the API serves it.</summary>
    internal async Task<StartFormViewModel> Form()
    {
        var catalog = new CatalogViewModel(new FakeClipboard(), Clock);
        catalog.Show(Api, Server, (await Server.Commands(Api, Token)).Value);
        return new StartFormViewModel(catalog, () => Api, Server, Files);
    }

    /// <summary>A form opened on the recipes <see cref="Rows"/> lists, with the first one chosen.</summary>
    internal async Task<StartFormViewModel> Opened(string? itemId = null)
    {
        var form = await Form();
        await form.Open(itemId, Token);
        await form.Choose(form.Recipes[0], Token);
        return form;
    }

    /// <summary>The list golden's row, once for each recipe named (id, standing, version): the golden's other members as served.</summary>
    internal void Rows(params (string Id, string Standing, int Version)[] rows)
    {
        var list = JsonNode.Parse(GoldenFiles.Api("workflow-recipes", "list"))!; // why: the golden is a JSON document
        var template = list["recipes"]![0]!; // why: the golden has a row
        list["recipes"] = new JsonArray([.. rows.Select(r => Row(template, r))]);
        Server.RecipeListAnswer = new RouteAnswer(HttpStatusCode.OK, list.ToJsonString());
    }

    /// <summary>Serves a recipe golden with its first-listed input changed, for every recipe read.</summary>
    internal void Bend(string recipe, Action<JsonObject> change)
    {
        var golden = JsonNode.Parse(WorkflowAnswers.Recipe(recipe, hash: null).Body)!; // why: the golden is a JSON document
        change(golden["inputs"]![0]!.AsObject()); // why: the recipe has an input
        Server.WorkflowAnswer = new RouteAnswer(HttpStatusCode.OK, golden.ToJsonString());
    }

    /// <summary>Serves the started golden with its existing and finished flags set.</summary>
    internal void Answers(bool existing, bool finished)
    {
        var golden = JsonNode.Parse(GoldenFiles.Api("workflow-instances-start", "started"))!; // why: the golden is a JSON document
        (golden["existing"], golden["finished"]) = (existing, finished);
        Server.StartAnswer = new RouteAnswer(HttpStatusCode.OK, golden.ToJsonString());
    }

    private static JsonNode Row(JsonNode template, (string Id, string Standing, int Version) row)
    {
        var clone = template.DeepClone();
        (clone["id"], clone["standing"], clone["version"]) = (row.Id, row.Standing, row.Version);
        _ = clone.AsObject().Remove("displayName");   // the golden's name is its own recipe's: these rows are other recipes, named by their ids here
        return clone;
    }
}
