using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Features.Sessions;
using Aw.Sindri.Testing;
using static Aw.Sindri.Tests.Sessions.SessionData;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>'sessions.scope' (issue 4164): under 'own' a scratch home lists only sessions on Sindri's own tmux sockets and sessions Sindri launched.</summary>
public sealed class SessionScopeTests
{
    private const string OwnSocket = "/tmp/tmux-1000/sindri-launch";
    private const string ForeignSocket = "/tmp/tmux-1000/default";
    private const string Launch = "LCH-0123456789abcdef0123456789abcdef";

    /// <summary>One claude under Sindri's own tmux server (411), one under a foreign tmux server (511), one plain foreign claude found through /proc (600), one launched claude with no tmux (700).</summary>
    private static CollectorHarness Box(SessionScope? scope)
    {
        var table = new FakeProcessTable()
            .With(Proc(400, 1, "tmux: server"), files: ["socket:[880001]"])
            .With(Proc(410, 400, "bash"))
            .With(Proc(411, 410, "claude", argv0: "claude"), cwd: "/w/own")
            .With(Proc(500, 1, "tmux: server"), files: ["socket:[880002]"])
            .With(Proc(510, 500, "bash"))
            .With(Proc(511, 510, "claude", argv0: "claude"), cwd: "/w/foreign")
            .With(Proc(601, 1, "bash"))
            .With(Proc(600, 601, "claude", argv0: "claude"), cwd: "/w/plain")
            .With(Proc(701, 1, "bash"))
            .With(Proc(700, 701, "claude", argv0: "claude"), SessionHostData.Env(("AW_SINDRI_LAUNCH", Launch)), cwd: "/w/launched");
        var tmux = new FakeTmuxServers()
            .With(OwnSocket, new TmuxServerView([new TmuxPane(410, "sindri-own", 0, 0, "%0")], []))
            .With(ForeignSocket, new TmuxServerView([new TmuxPane(510, "lane", 0, 0, "%0")], []));
        var sockets = new FakeUnixSockets(new Dictionary<long, string> { [880001] = OwnSocket, [880002] = ForeignSocket });
        var hosts = new SessionHosts(table, sockets, tmux, new FakeTeamNames(), new FakeTerminalNames());
        var settings = scope is { } s ? SessionSettings.Defaults with { Scope = s } : SessionSettings.Defaults;
        return new CollectorHarness(settings, table, hosts: hosts);
    }

    private static async Task<int[]> Pids(CollectorHarness box)
    {
        _ = await box.Pass();
        return [.. box.Snapshot.Current!.Sessions.Select(static s => s.Pid).Order()];
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Scope_own_lists_no_row_for_a_foreign_tmux_server_or_a_foreign_claude_process()
    {
        Assert.Equal([411, 700], await Pids(Box(SessionScope.Own)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Scope_all_and_an_absent_scope_list_every_session_as_before()
    {
        Assert.Equal([411, 511, 600, 700], await Pids(Box(SessionScope.All)));
        Assert.Equal([411, 511, 600, 700], await Pids(Box(scope: null)));
    }
}
