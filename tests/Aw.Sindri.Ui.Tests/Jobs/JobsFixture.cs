using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Json;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Jobs;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Jobs;

/// <summary>What the Jobs tests share: the API's goldens as the served list and job, a clock five minutes after the goldens' times, and edits of a golden as JSON.</summary>
internal sealed class JobsFixture
{
    internal const string Id = "JOB-00000000000000000000000000000001";

    internal static readonly Uri Url = new("http://127.0.0.1:7410/");

    internal static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-01-01T00:05:00Z", System.Globalization.CultureInfo.InvariantCulture);

    internal FakeSindriApi Server { get; } = new();

    internal FakeClipboard Clipboard { get; } = new();

    internal FakeTimeProvider Clock { get; } = new(Now);

    internal CatalogViewModel Catalog { get; }

    internal JobsFixture(bool catalog = true)
    {
        Catalog = new CatalogViewModel(Clipboard, Clock);
        if (catalog)
        {
            ShowCatalog();
        }
    }

    internal void ShowCatalog() =>
        Catalog.Show(Url, Server, AwJson.Deserialize<CommandList>(System.Text.Encoding.UTF8.GetBytes(GoldenFiles.Api("commands", "catalog")), SindriApiClient.Json).Value);

    internal static JsonNode Golden(string slug, string scenario) => JsonNode.Parse(GoldenFiles.Api(slug, scenario))!; // why: the golden is a JSON document

    internal void ServeList(string scenario) => Server.JobsAnswer = RouteAnswer.Api("jobs", scenario);

    internal void ServeList(JsonNode list) => Server.JobsAnswer = new RouteAnswer(HttpStatusCode.OK, list.ToJsonString());

    internal void ServeJob(string scenario) => Server.JobAnswers[Id] = RouteAnswer.Api("jobs-id", scenario);

    internal JobsPageViewModel Page() => new(Server, Catalog, Clock);

    /// <summary>A page that is shown and has read the list once.</summary>
    internal async Task<JobsPageViewModel> Shown(CancellationToken token)
    {
        var page = Page();
        page.SetShown(shown: true);
        await page.Refresh(Url, token);
        return page;
    }

    internal JobSheetViewModel Sheet()
    {
        var sheet = new JobSheetViewModel(Server);
        sheet.Wire(Catalog, Clock);
        return sheet;
    }
}
