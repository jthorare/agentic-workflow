using System.Net;
using System.Text.Json.Nodes;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Linear;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Linear;

/// <summary>What the "New issue" dialog's tests share: a fake that serves the two-team connection and the ENG lookups, a clock, and short ways to pick and read a field.</summary>
internal sealed class NewLinearIssueFixture
{
    internal static readonly Uri Url = new("http://127.0.0.1:7410/");

    internal NewLinearIssueFixture()
    {
        Vm = new NewLinearIssueViewModel(Server, () => Url, Clock);
    }

    internal FakeSindriApi Server { get; } = new();

    internal FakeTimeProvider Clock { get; } = new(DateTimeOffset.Parse("2026-01-01T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

    internal NewLinearIssueViewModel Vm { get; }

    /// <summary>Runs a test body on a pumped UI thread, with the fixture built there.</summary>
    internal static Task Run(Func<NewLinearIssueFixture, Task> body, CancellationToken token) =>
        UiThread.Run(() => body(new NewLinearIssueFixture()), token);

    /// <summary>The connection golden with only the ENG team.</summary>
    internal void OneTeam()
    {
        var connection = JsonNode.Parse(GoldenFiles.Api("linear-connection", "connected"))!.AsObject();
        connection["teams"] = new JsonArray(connection["teams"]!.AsArray()[0]!.DeepClone());
        Server.LinearConnectionAnswer = new RouteAnswer(HttpStatusCode.OK, connection.ToJsonString());
    }

    /// <summary>Opens the dialog with one team and waits for that team's lookups.</summary>
    internal async Task<NewLinearIssueViewModel> OpenedOnEng(CancellationToken token)
    {
        OneTeam();
        await Vm.Open(token);
        await Vm.Reading;
        return Vm;
    }

    /// <summary>Picks a team by its key and waits for its lookups.</summary>
    internal async Task PickTeam(string key)
    {
        Vm.Team = Vm.TeamChoices.Single(t => string.Equals(t.Value?.GetValue<string>(), key, StringComparison.Ordinal));
        await Vm.Reading;
    }

    /// <summary>The entry of a list with this name.</summary>
    internal static LinearChoiceViewModel Entry(IReadOnlyList<LinearChoiceViewModel> list, string name) =>
        list.Single(e => string.Equals(e.Name, name, StringComparison.Ordinal));

    /// <summary>The names of a list, in order.</summary>
    internal static string[] Names(IReadOnlyList<LinearChoiceViewModel> list) => [.. list.Select(e => e.Name)];

    /// <summary>A create golden answered as served: a 200 body, or the refusal with the status it names.</summary>
    internal static RouteAnswer Create(string scenario) =>
        GoldenFiles.Api("linear-create", scenario).Contains("\"status\"", StringComparison.Ordinal) ? RouteAnswer.Problem("linear-create", scenario) : RouteAnswer.Api("linear-create", scenario);

    /// <summary>A 429 SND-LINEAR-012 that says to try again at 10:47 UTC.</summary>
    internal static RouteAnswer RateLimited() =>
        new(HttpStatusCode.TooManyRequests, """{"type":"about:blank","title":"SND-LINEAR-012","status":429,"detail":"Linear's rate limit","traceId":"<trace>","retryAfter":"2026-01-01T10:47:00.0000000Z"}""");

    /// <summary>Lets a gated create go and waits for the dialog to draw the answer.</summary>
    internal static async Task Release(TaskCompletionSource gate, Task sending)
    {
        gate.SetResult();
        await sending;
    }
}
