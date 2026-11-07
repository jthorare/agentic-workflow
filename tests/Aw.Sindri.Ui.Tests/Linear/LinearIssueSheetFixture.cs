using System.Net;
using System.Text.Json.Nodes;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Linear;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Linear;

/// <summary>What the issue sheet's tests share: the API's linear-issue goldens as the served issue (edited per case), the fake launcher and a clock at the goldens' "now".</summary>
internal sealed class LinearIssueSheetFixture
{
    internal static readonly Uri Url = new("http://127.0.0.1:7410/");

    internal LinearIssueSheetFixture()
    {
        Sheet = new LinearIssueSheetViewModel(Server, Launcher, Clock);
    }

    internal FakeSindriApi Server { get; } = new();

    internal FakeLauncher Launcher { get; } = new();

    internal FakeTimeProvider Clock { get; } = new(DateTimeOffset.Parse("2026-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

    internal LinearIssueSheetViewModel Sheet { get; }

    internal static JsonObject Golden(string scenario) => JsonNode.Parse(GoldenFiles.Api("linear-issue", scenario))!.AsObject();   // why: the golden is a JSON object

    internal static RouteAnswer Served(JsonNode issue) => new(HttpStatusCode.OK, issue.ToJsonString());

    /// <summary>The not-found golden as the 404 the API serves.</summary>
    internal static RouteAnswer NotFound() => new(HttpStatusCode.NotFound, GoldenFiles.Api("linear-issue", "not-found"));

    /// <summary>Opens the sheet on a key and reads it.</summary>
    internal async Task<LinearIssueSheetViewModel> Opened(string key, CancellationToken token)
    {
        await Sheet.Open(Url, key, token);
        return Sheet;
    }
}
