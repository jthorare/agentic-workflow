using System.Net;
using System.Text.Json.Nodes;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Reviews;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.ReviewPage;

/// <summary>What the review page tests share: the API's reviews-pr goldens as the served page, a clock at the goldens' "now", and edits of a golden as JSON.</summary>
internal sealed class ReviewPageFixture
{
    internal static readonly Uri Url = new("http://127.0.0.1:7410/");

    internal const string Repo = "acme/api";

    internal const int Number = 2071;

    internal FakeSindriApi Server { get; } = new();

    internal FakeTimeProvider Clock { get; } = new(DateTimeOffset.Parse("2026-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

    internal ReviewPanelMemory Memory { get; } = new();

    internal static JsonNode Golden(string scenario) => JsonNode.Parse(GoldenFiles.Api("reviews-pr", scenario))!; // why: the golden is a JSON document

    internal void Serve(string scenario) => Server.ReviewPageAnswer = RouteAnswer.Api("reviews-pr", scenario);

    internal void Serve(JsonNode page) => Server.ReviewPageAnswer = new RouteAnswer(HttpStatusCode.OK, page.ToJsonString());

    /// <summary>A refusal golden, served with the status it names.</summary>
    internal void Refuse(string scenario) => Server.ReviewPageAnswer = RouteAnswer.Problem("reviews-pr", scenario);

    internal ReviewPageViewModel Page(bool wide = true) => new(Server, Clock, Memory) { IsWide = wide };

    /// <summary>A page that has read the PR once.</summary>
    internal async Task<ReviewPageViewModel> Opened(CancellationToken token, bool wide = true)
    {
        var page = Page(wide);
        await page.Open(Url, Repo, Number, token);
        return page;
    }

    /// <summary>The golden with one JSON edit, opened.</summary>
    internal async Task<ReviewPageViewModel> Opened(string scenario, Action<JsonNode> edit, CancellationToken token, bool wide = true)
    {
        var json = Golden(scenario);
        edit(json);
        Serve(json);
        return await Opened(token, wide);
    }
}
