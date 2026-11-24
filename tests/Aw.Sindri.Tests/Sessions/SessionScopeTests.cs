using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Features.Sessions;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;
using static Aw.Sindri.Tests.Sessions.SessionData;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>'sessions.scope' (issue 4164): under 'own' a scratch home lists only sessions on sockets under its own tmux directory and sessions launched from its own launch records.</summary>
public sealed class SessionScopeTests
{
    private const string OwnDirectory = "/scratch/home/tmux";
    private const string OwnSocket = OwnDirectory + "/tmux-1000/default";   // named 'default' on purpose: the path decides, not the name
    private const string LaneSocket = "/tmp/tmux-1000/sindri-v4";
    private const string OtherHomeSocket = "/other/home/tmux/tmux-1000/sindri";   // socket kind 'sindri', another home's directory
    private const string OwnLaunch = "LCH-0123456789abcdef0123456789abcdef";
    private const string OtherLaunch = "LCH-fedcba9876543210fedcba9876543210";

    /// <summary>
    /// A claude on this home's socket (411), one on a lane socket (511), one on another home's 'sindri' socket (811), a plain foreign claude (600),
    /// one launched from this home's records (700) and one launched by another home (900).
    /// </summary>
    private static CollectorHarness Box(SessionSettings settings)
    {
        var table = new FakeProcessTable()
            .With(Proc(400, 1, "tmux: server"), files: ["socket:[880001]"])
            .With(Proc(410, 400, "bash"))
            .With(Proc(411, 410, "claude", argv0: "claude"), cwd: "/w/own")
            .With(Proc(500, 1, "tmux: server"), files: ["socket:[880002]"])
            .With(Proc(510, 500, "bash"))
            .With(Proc(511, 510, "claude", argv0: "claude"), cwd: "/w/lane")
            .With(Proc(800, 1, "tmux: server"), files: ["socket:[880003]"])
            .With(Proc(810, 800, "bash"))
            .With(Proc(811, 810, "claude", argv0: "claude"), cwd: "/w/other-home")
            .With(Proc(601, 1, "bash"))
            .With(Proc(600, 601, "claude", argv0: "claude"), cwd: "/w/plain")
            .With(Proc(701, 1, "bash"))
            .With(Proc(700, 701, "claude", argv0: "claude"), SessionHostData.Env(("AW_SINDRI_LAUNCH", OwnLaunch)), cwd: "/w/launched")
            .With(Proc(901, 1, "bash"))
            .With(Proc(900, 901, "claude", argv0: "claude"), SessionHostData.Env(("AW_SINDRI_LAUNCH", OtherLaunch)), cwd: "/w/other-launch");
        var tmux = new FakeTmuxServers()
            .With(OwnSocket, new TmuxServerView([new TmuxPane(410, "own", 0, 0, "%0")], []))
            .With(LaneSocket, new TmuxServerView([new TmuxPane(510, "lane", 0, 0, "%0")], []))
            .With(OtherHomeSocket, new TmuxServerView([new TmuxPane(810, "other", 0, 0, "%0")], []));
        var sockets = new FakeUnixSockets(new Dictionary<long, string> { [880001] = OwnSocket, [880002] = LaneSocket, [880003] = OtherHomeSocket });
        var launches = new FakeLaunchFiles();
        launches.Open(LaunchId.From(OwnLaunch));
        var hosts = new SessionHosts(table, sockets, tmux, new FakeTeamNames(), new FakeTerminalNames(), launches);
        return new CollectorHarness(settings, table, hosts: hosts);
    }

    private static SessionSettings Settings(SessionScope? scope) =>
        scope is { } s ? SessionSettings.Defaults with { Scope = s, OwnTmuxDirectory = OwnDirectory } : SessionSettings.Defaults;

    private static async Task<int[]> Pids(CollectorHarness box)
    {
        _ = await box.Pass();
        return [.. box.Snapshot.Current!.Sessions.Select(static s => s.Pid).Order()];
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Scope_own_lists_no_row_for_a_foreign_tmux_server_or_a_foreign_claude_process() =>
        Assert.Equal([411, 700], await Pids(Box(Settings(SessionScope.Own))));

    [Fact, Trait("Lane", "unit")]
    public async Task Scope_own_gives_no_row_to_a_lane_socket_or_another_homes_sindri_socket()
    {
        var box = Box(Settings(SessionScope.Own));

        var pids = await Pids(box);

        Assert.DoesNotContain(511, pids);   // /tmp/tmux-1000/sindri-v4
        Assert.DoesNotContain(811, pids);   // another home's 'sindri': socket kind 'sindri', not this home's directory
        Assert.DoesNotContain(900, pids);   // a launch id that is no launch record of this home
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Scope_all_and_an_absent_scope_list_every_session_as_before()
    {
        int[] everything = [411, 511, 600, 700, 811, 900];

        Assert.Equal(everything, await Pids(Box(Settings(SessionScope.All))));
        Assert.Equal(everything, await Pids(Box(Settings(scope: null))));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_scope_typo_in_launcher_json_lists_no_foreign_row() =>
        Assert.Equal([411, 700], await Pids(Box(LauncherConfigFileTests.LoadText("""{ "schema": 1, "sessions": { "scope": "onw" } }""").Sessions with { OwnTmuxDirectory = OwnDirectory })));
}
