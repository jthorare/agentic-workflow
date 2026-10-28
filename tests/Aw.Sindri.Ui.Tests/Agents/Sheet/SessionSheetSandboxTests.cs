using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Agents;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Agents.Sheet;

/// <summary>The session sheet's Sandbox line and headroom (sandbox spec §8, states.json 'sandboxLine'): one fact per state, over the spec-shaped session rows.</summary>
[Lane(TestLane.Unit)]
public sealed class SessionSheetSandboxTests
{
    private const string Stepped = "claude-code/aaaaaaaa-0000-4000-8000-000000000101";
    private const string Attended = "claude-code/aaaaaaaa-0000-4000-8000-000000000102";
    private const string Unreadable = "claude-code/aaaaaaaa-0000-4000-8000-000000000103";
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeSindriApi _server = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // The one-session read of a spec-shaped row: the row inside the sheet's envelope.
    private static RouteAnswer One(string id)
    {
        var rows = JsonNode.Parse(GoldenFiles.SpecShaped("sessions", "sandbox-rows"))!["sessions"]!.AsArray();
        var row = rows.Single(r => string.Equals(r!["id"]!.GetValue<string>(), id, StringComparison.Ordinal))!.DeepClone();
        var body = new JsonObject { ["session"] = row, ["cwd"] = "/work/api", ["stateHistory"] = new JsonArray(), ["asOf"] = "2026-10-28T09:00:00.0000000Z" };
        return new RouteAnswer(HttpStatusCode.OK, body.ToJsonString());
    }

    private async Task<SessionSheetViewModel> Opened(string reference)
    {
        _server.SessionOneBody = (_, id) => One(id);
        var sheet = new SessionSheetViewModel(_server);
        sheet.Wire(new Aw.Sindri.Ui.ViewModels.Controls.CatalogViewModel(new FakeClipboard(), new FakeTimeProvider(A.Noon)), new FakeTimeProvider(A.Noon));
        await sheet.Open(_api, reference, Token);
        return sheet;
    }

    [Fact]
    public async Task A_step_says_it_is_sandboxed_and_where_it_writes_with_its_memory_as_text()
    {
        var sheet = await Opened(Stepped);

        Assert.Equal("Sandbox: on, writes only to /home/thor/repos/aw-wt/api-sb6", sheet.SandboxLine);
        Assert.Equal("memory 2.1 of 12 GiB", sheet.SandboxHeadroom);
        Assert.False(sheet.IsSandboxDimmed);
    }

    [Fact]
    public async Task A_session_you_open_says_attended_and_a_memory_not_served_leaves_the_headroom_out()
    {
        var sheet = await Opened(Attended);

        Assert.Equal("Sandbox: on, attended, writes only to /home/thor/repos/aw-wt/api-sb6", sheet.SandboxLine);
        Assert.Equal(string.Empty, sheet.SandboxHeadroom);
    }

    [Fact]
    public async Task A_plan_that_does_not_read_says_details_unavailable()
    {
        var sheet = await Opened(Unreadable);

        Assert.Equal(("Sandbox: details unavailable", string.Empty, false), (sheet.SandboxLine, sheet.SandboxHeadroom, sheet.IsSandboxDimmed));
    }

    [Fact]
    public async Task A_session_with_no_sindri_launch_has_no_sandbox_line()
    {
        var sheet = new SessionSheetViewModel(_server);
        await sheet.Open(_api, "claude-code/aaaaaaaa-0000-4000-8000-000000000001", Token);

        Assert.Equal((string.Empty, string.Empty), (sheet.SandboxLine, sheet.SandboxHeadroom));
    }

    [Fact]
    public async Task When_serve_goes_down_the_line_says_status_unavailable_dimmed_and_the_headroom_goes()
    {
        var sheet = await Opened(Stepped);
        _server.SessionAnswer = RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-API-001", "Sindri is not running.");
        _server.SessionOneBody = null;

        await sheet.Refresh(_api, Token);

        Assert.Equal(("Sandbox: status unavailable", string.Empty, true), (sheet.SandboxLine, sheet.SandboxHeadroom, sheet.IsSandboxDimmed));
    }

    [Fact]
    public async Task Serve_coming_back_draws_the_line_again()
    {
        var sheet = await Opened(Stepped);
        _server.SessionAnswer = RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-API-001", "Sindri is not running.");
        _server.SessionOneBody = null;
        await sheet.Refresh(_api, Token);
        _server.SessionAnswer = null;
        _server.SessionOneBody = (_, id) => One(id);

        await sheet.Refresh(_api, Token);

        Assert.Equal(("Sandbox: on, writes only to /home/thor/repos/aw-wt/api-sb6", false), (sheet.SandboxLine, sheet.IsSandboxDimmed));
    }
}
