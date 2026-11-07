using System.Net;
using System.Text.Json.Nodes;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Linear;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Linear;

/// <summary>What the Linear view's tests share: the API's goldens as the served pages (mine and teams), a clock at the goldens' "now", and edits of a golden as JSON.</summary>
internal sealed class LinearFixture
{
    internal static readonly Uri Url = new("http://127.0.0.1:7410/");

    internal LinearFixture()
    {
        Prefs = new Preferences(Store);
        Server.LinearIssuesFor = query => string.Equals(query.Group, "teams", StringComparison.Ordinal) ? Teams : Mine;
    }

    internal FakeSindriApi Server { get; } = new();

    internal FakeTimeProvider Clock { get; } = new(DateTimeOffset.Parse("2026-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

    internal InMemoryPreferencesStore Store { get; } = new();

    internal Preferences Prefs { get; }

    /// <summary>What a mine query is answered with.</summary>
    internal RouteAnswer Mine { get; set; } = RouteAnswer.Api("linear-issues", "mine");

    /// <summary>What a teams query is answered with.</summary>
    internal RouteAnswer Teams { get; set; } = RouteAnswer.Api("linear-issues", "teams-page2");

    internal static JsonNode Golden(string scenario) => JsonNode.Parse(GoldenFiles.Api("linear-issues", scenario))!; // why: the golden is a JSON document

    internal static RouteAnswer Served(JsonNode answer) => new(HttpStatusCode.OK, answer.ToJsonString());

    /// <summary>The filtered-empty golden for a group.</summary>
    internal static RouteAnswer Nothing(string group)
    {
        var empty = Golden("filtered-empty");
        empty["group"] = group;
        return Served(empty);
    }

    internal LinearWorkViewModel Work() => new(Server, Prefs, Clock);

    /// <summary>A view on the Linear tab, Work shown, that has read the list once.</summary>
    internal async Task<LinearWorkViewModel> Shown(CancellationToken token)
    {
        var work = Work();
        work.Choose(linear: true);
        work.SetShown(shown: true);
        await work.Refresh(Url, token);
        return work;
    }
}
