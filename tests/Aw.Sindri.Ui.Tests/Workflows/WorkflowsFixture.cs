using System.Net;
using System.Text.Json.Nodes;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>What the Workflows tests share: the API's goldens as the served reads, a clock five minutes after the goldens' times, and edits of a golden as JSON.</summary>
internal sealed class WorkflowsFixture
{
    internal const string Slug = "morning-check";

    internal const string RunId = "morning-check@1:1";

    internal static readonly Uri Url = new("http://127.0.0.1:7410/");

    internal static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-01-01T00:05:00Z", System.Globalization.CultureInfo.InvariantCulture);

    internal FakeSindriApi Server { get; } = new();

    internal FakeTimeProvider Clock { get; } = new(Now);

    internal static JsonNode Golden(string slug, string scenario) => JsonNode.Parse(GoldenFiles.Api(slug, scenario))!; // why: the golden is a JSON document

    internal WorkflowsFixture()
    {
        Server.WorkflowAnswers[Slug] = RouteAnswer.Api("workflows-slug", "found");
        Server.WorkflowRunsAnswers[Slug] = RouteAnswer.Api("workflows-slug-runs", "list-three");
        Server.WorkflowRunAnswers[RunId] = RouteAnswer.Api("workflow-runs-run", "finished");
    }

    internal void ServeList(JsonNode list) => Server.WorkflowsAnswer = new RouteAnswer(HttpStatusCode.OK, list.ToJsonString());

    internal void ServeRuns(string scenario) => Server.WorkflowRunsAnswers[Slug] = RouteAnswer.Api("workflows-slug-runs", scenario);

    internal void ServeRun(string scenario) => Server.WorkflowRunAnswers[RunId] = RouteAnswer.Api("workflow-runs-run", scenario);

    internal void ServeRun(JsonNode run) => Server.WorkflowRunAnswers[RunId] = new RouteAnswer(HttpStatusCode.OK, run.ToJsonString());

    internal WorkflowsPageViewModel Page() => new(Server, Clock);

    /// <summary>A page that is shown and has read the list once.</summary>
    internal async Task<WorkflowsPageViewModel> Shown(CancellationToken token)
    {
        var page = Page();
        await page.Show(Url, token);
        return page;
    }

    /// <summary>A page that has opened the run: its detail is read.</summary>
    internal async Task<RunDetailViewModel> OpenedRun(CancellationToken token)
    {
        var page = await Shown(token);
        await page.OpenWorkflow(Slug, token);
        await page.OpenRun(RunId, token);
        return page.Run;
    }
}
