using System.Net;
using System.Text.Json.Nodes;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Linear;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Linear;

/// <summary>What the issue sheet's edit tests share: the writable ENG-12 golden and the ENG lookups on the fake, a clock, and short ways to pick, tick and read a field.</summary>
internal sealed class LinearIssueEditFixture
{
    internal const string Done = "00000000-0000-4000-8000-000000000055";

    internal static readonly Uri Url = new("http://127.0.0.1:7410/");

    internal LinearIssueEditFixture()
    {
        Server.LinearIssueAnswer = LinearIssueSheetFixture.Served(Issue("writable"));
        Sheet = new LinearIssueSheetViewModel(Server, Launcher, Clock);
    }

    internal FakeSindriApi Server { get; } = new();

    internal FakeLauncher Launcher { get; } = new();

    internal FakeTimeProvider Clock { get; } = new(DateTimeOffset.Parse("2026-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

    internal LinearIssueSheetViewModel Sheet { get; }

    internal List<LinearIssueFocus> Focus { get; } = [];

    /// <summary>A linear-issue golden as an object to edit.</summary>
    internal static JsonObject Issue(string scenario) => LinearIssueSheetFixture.Golden(scenario);

    /// <summary>The 10:05 "now" the applied golden carries, in the viewer's time zone.</summary>
    internal static string Local(string utc) => DateTimeOffset.Parse(utc, System.Globalization.CultureInfo.InvariantCulture).ToLocalTime().ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Serves this issue body from the read route.</summary>
    internal void Serve(JsonObject issue) => Server.LinearIssueAnswer = LinearIssueSheetFixture.Served(issue);

    /// <summary>Opens ENG-12 and reads it and its lookups.</summary>
    internal async Task<LinearIssueSheetViewModel> Opened(CancellationToken token)
    {
        Sheet.FocusRequested += (_, e) => Focus.Add(e.Value);
        await Sheet.Open(Url, "ENG-12", token);
        return Sheet;
    }

    internal LinearEditFieldViewModel Field(string label) => Sheet.EditFields.Single(f => string.Equals(f.Label, label, StringComparison.Ordinal));

    /// <summary>Picks an option of a picker by its name.</summary>
    internal void Pick(string label, string option)
    {
        var field = Field(label);
        field.Selected = field.Options.Single(o => string.Equals(o.Name, option, StringComparison.Ordinal));
    }

    /// <summary>Ticks or unticks a label.</summary>
    internal void Tick(string name, bool on) => Field("Labels").Options.Single(o => string.Equals(o.Name, name, StringComparison.Ordinal)).IsTicked = on;

    /// <summary>The one edit the fake was sent.</summary>
    internal LinearChangeSent Sent => Server.LinearChanges.Single();

    /// <summary>A change golden answered as served: a 200 body, or the refusal with the status it names.</summary>
    internal static RouteAnswer Change(string scenario) =>
        GoldenFiles.Api("linear-change", scenario).Contains("\"status\"", StringComparison.Ordinal) ? RouteAnswer.Problem("linear-change", scenario) : RouteAnswer.Api("linear-change", scenario);

    /// <summary>The 409 golden with its fields and live priority set: Linear changed the priority from 1 to <paramref name="linear"/>.</summary>
    internal static RouteAnswer PriorityConflict(int linear)
    {
        var body = JsonNode.Parse(GoldenFiles.Api("linear-change", "conflict"))!.AsObject();
        body["fields"] = new JsonArray(new JsonObject { ["field"] = "priority", ["opened"] = 1, ["linear"] = linear });
        body["issue"]!["priority"] = linear;
        return new(HttpStatusCode.Conflict, body.ToJsonString());
    }
}
