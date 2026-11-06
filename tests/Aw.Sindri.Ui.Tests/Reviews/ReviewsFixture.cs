using System.Net;
using System.Text.Json.Nodes;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Reviews;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Reviews;

/// <summary>What the Reviews tests share: the API's goldens as the served list, a clock at the goldens' "now", and edits of a golden as JSON.</summary>
internal sealed class ReviewsFixture
{
    internal static readonly Uri Url = new("http://127.0.0.1:7410/");

    internal FakeSindriApi Server { get; } = new();

    internal FakeTimeProvider Clock { get; } = new(DateTimeOffset.Parse("2026-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

    internal static JsonNode Golden(string scenario) => JsonNode.Parse(GoldenFiles.Api("reviews", scenario))!; // why: the golden is a JSON document

    internal void Serve(string scenario) => Server.ReviewsAnswer = RouteAnswer.Api("reviews", scenario);

    internal void Serve(JsonNode list) => Server.ReviewsAnswer = new RouteAnswer(HttpStatusCode.OK, list.ToJsonString());

    internal ReviewsPageViewModel Page() => new(Server, Clock);

    /// <summary>A page that is shown and has read the list once.</summary>
    internal async Task<ReviewsPageViewModel> Shown(CancellationToken token)
    {
        var page = Page();
        page.SetShown(shown: true);
        await page.Refresh(Url, token);
        return page;
    }
}
