using Aw.Kernel;
using Aw.Sindri.Ui.Api.Sessions;

namespace Aw.Sindri.Ui.Testing;

/// <summary>
/// The contract (<see cref="SindriApiContract"/>, continued): the session reads (collector spec CA-1, CA-2) as the goldens serve them,
/// so the fake and the real client both read what Sindri writes.
/// </summary>
public abstract partial class SindriApiContract
{
    private const string ClaudeTmux = "claude-code/aaaaaaaa-0000-4000-8000-000000000001";

    /// <summary>The three 503 bodies of SND-SESSION-002.</summary>
    public static TheoryData<string> UnavailableScenarios => ["disabled", "proc-unreadable", "unsupported-platform"];

    private static (string Provider, string Id) Split(string reference) => reference.Split('/') is [var provider, var id] ? (provider, id) : throw new FormatException(reference);

    [Fact]
    public async Task Sessions_answers_every_golden_row_and_the_served_counts()
    {
        var golden = GoldenFiles.Node("sessions", "fixture-three-providers");

        var list = (await Reach(new FakeSindriApi()).Sessions(_api, new SessionQuery(), Token)).Value;

        Expect.Sequence(golden["sessions"]!.AsArray().Select(r => (string)r!["ref"]!), list.Sessions.Select(s => s.Ref));
        var counts = golden["counts"]!;
        Expect.Equal(((int)counts["live"]!, (int)counts["asking"]!, (int)counts["outsideWorkflow"]!), (list.Counts.Live, list.Counts.Asking, list.Counts.OutsideWorkflow));
        var asking = list.Sessions.Single(s => s.Asking is not null);
        Expect.Equal(((string)golden["sessions"]![1]!["asking"]!["reason"]!, (string)golden["sessions"]![1]!["asking"]!["tool"]!), (asking.Asking!.Reason, asking.Asking.Tool));
        Expect.True(list.Sessions.Single(s => s.LaunchId is not null).Ref.StartsWith("cursor/", StringComparison.Ordinal));
        Expect.Equal(expected: null, list.Sessions.Single(s => s.Ref.StartsWith("codex/", StringComparison.Ordinal)).StartedAt);
    }

    [Fact]
    public async Task Sessions_empty_returns_the_served_sentence()
    {
        var server = new FakeSindriApi { SessionAnswer = RouteAnswer.Api("sessions", "empty") };

        var list = (await Reach(server).Sessions(_api, new SessionQuery(), Token)).Value;

        Expect.Equal((string)GoldenFiles.Node("sessions", "empty")["empty"]!, list.Empty);
        Expect.Equal(0, list.Sessions.Count);
    }

    [Fact]
    public async Task Sessions_reads_the_recently_ended_with_their_resume_and_the_problems()
    {
        var ended = (await Reach(new FakeSindriApi { SessionAnswer = RouteAnswer.Api("sessions", "recently-ended") }).Sessions(_api, new SessionQuery(), Token)).Value;
        var problems = (await Reach(new FakeSindriApi { SessionAnswer = RouteAnswer.Api("sessions", "problems-source-failed") }).Sessions(_api, new SessionQuery(), Token)).Value;

        var one = ended.RecentlyEnded.Single();
        Expect.Equal(ClaudeTmux, one.Session.Ref);
        Expect.Sequence(GoldenFiles.Node("sessions", "recently-ended")["recentlyEnded"]![0]!["resume"]!.AsArray().Select(a => (string)a!), one.Resume);
        var served = GoldenFiles.Node("sessions", "problems-source-failed")["problems"]![0]!;
        Expect.Equal(((string)served["source"]!, (string)served["code"]!, (string)served["text"]!), (problems.Problems[0].Source, problems.Problems[0].Code, problems.Problems[0].Text));
    }

    [Theory]
    [MemberData(nameof(UnavailableScenarios))]
    public async Task Sessions_002_is_a_refusal_with_its_detail(string scenario)
    {
        var server = new FakeSindriApi { SessionAnswer = RouteAnswer.Problem("sessions", scenario) };

        var list = await Reach(server).Sessions(_api, new SessionQuery(), Token);

        Expect.Equal(new Error("SND-SESSION-002", ErrorKind.Unavailable, (string)GoldenFiles.Node("sessions", scenario)["detail"]!), list.Error);
    }

    [Fact]
    public async Task Session_answers_claude_tmux_with_cwd_socket_and_history()
    {
        var golden = GoldenFiles.Node("sessions-id", "claude-tmux");
        var (provider, id) = Split(ClaudeTmux);

        var view = (await Reach(new FakeSindriApi()).Session(_api, provider, id, Token)).Value;

        Expect.Equal(ClaudeTmux, view.Session.Ref);
        Expect.Equal(((string)golden["cwd"]!, (string)golden["session"]!["host"]!["socket"]!), (view.Cwd, view.Session.Host.Socket));
        Expect.Sequence(golden["stateHistory"]!.AsArray().Select(e => (string)e!["state"]!), view.StateHistory!.Select(e => e.State));
    }

    [Fact]
    public async Task Session_unknown_ref_is_the_404_refusal()
    {
        var result = await Reach(new FakeSindriApi()).Session(_api, "claude-code", "not-a-session", Token);

        Expect.Equal(new Error("SND-SESSION-404", ErrorKind.NotFound, (string)GoldenFiles.Node("sessions-id", "not-found")["detail"]!), result.Error);
    }

    [Fact]
    public async Task A_session_caller_view_carries_the_human_fields()
    {
        var server = new FakeSindriApi { SessionAnswer = RouteAnswer.Api("sessions-id", "session-caller-hidden-fields") };
        var (provider, id) = Split(ClaudeTmux);

        var view = (await Reach(server).Session(_api, provider, id, Token)).Value;

        Expect.True(view.Session.Title is not null && view.Session.Pid is not null && view.Session.Host.Session is not null && view.Session.Host.Pane is not null);
        Expect.True(view.Cwd is not null && view.StateHistory is not null);
    }

    [Fact]
    public async Task A_session_read_carries_the_status_of_a_down_server()
    {
        var server = new FakeSindriApi();
        server.GoDown();

        Expect.Equal("SND-UI-002", (await Reach(server).Sessions(_api, new SessionQuery(), Token)).Error.Code);
    }
}
