using Aw.Sindri.Core.Sessions;
using static Aw.Sindri.Tests.Sessions.SessionData;
using static Aw.Sindri.Tests.Sessions.SessionHostData;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>Which host shows a session: tmux with its socket, Warp, a terminals.json terminal, ssh (collector spec CO-2b, §6.1).</summary>
public sealed class HostRulesTests
{
    private const string Socket = "/tmp/tmux-1000/t1";

    private static HostInfo Detect(FakeHostSources sources, ProcessEntry agent, IEnumerable<ProcessEntry> table, params (string Name, string Value)[] environment) =>
        HostRules.Detect(new HostQuery(table.ToDictionary(static p => p.Pid), agent, Env(environment)), sources);

    private static ProcessEntry[] TmuxTree() =>
    [
        Proc(400, 1, "tmux: server"), Proc(410, 400, "bash"), Proc(411, 410, "claude"),
        Proc(300, 1, "kitty"), Proc(305, 300, "bash"), Proc(301, 305, "tmux: client", tty: 34817),
    ];

    private static FakeHostSources TmuxSources(string session = "api")
    {
        var sources = new FakeHostSources();
        sources.Sockets[400] = Socket;
        sources.Views[Socket] = new TmuxServerView([new TmuxPane(410, session, 0, 0, "%0")], [new TmuxClient(301, "/dev/pts/1", session)]);
        sources.Environments[301] = Env();
        sources.Environments[305] = Env(("KITTY_WINDOW_ID", "7"), ("KITTY_LISTEN_ON", "unix:/tmp/kitty-1000"));
        return sources;
    }

    [Fact, Trait("Lane", "unit")]
    public void HostRules_finds_the_tmux_server_and_its_socket()
    {
        var table = TmuxTree();

        var host = Detect(TmuxSources(), table[2], table);

        Assert.Equal(("tmux", "tmux · kitty"), (host.Kind, host.Label));
        Assert.Equal(new TmuxFacts(Socket, "other", "api", "%0") { AttachedClients = 1 }, host.Tmux);
    }

    [Fact, Trait("Lane", "unit")]
    public void HostRules_takes_terminal_facts_from_the_client_chain_only()
    {
        var table = TmuxTree();
        var sources = TmuxSources();
        // the agent inherited Warp's variables through the tmux server; they say nothing about the terminal that shows it
        var host = Detect(sources, table[2], table, ("TERM_PROGRAM", "WarpTerminal"), ("WARP_FOCUS_URL", "warp://session/0123456789abcdef0123456789abcdef"));

        Assert.Equal("kitty", host.Terminal?.Name);
        Assert.Equal("7", host.Terminal?.KittyWindow);
        Assert.Null(host.Terminal?.WarpFocusUrl);
    }

    [Fact, Trait("Lane", "unit")]
    public void HostRules_keeps_tmux_with_no_place_when_the_server_cannot_be_read()
    {
        var table = TmuxTree();
        var sources = new FakeHostSources();
        sources.Sockets[400] = Socket;

        var host = Detect(sources, table[2], table, ("TMUX", Socket + ",400,0"));

        Assert.Equal(("tmux", "tmux"), (host.Kind, host.Label));
        Assert.Null(host.Tmux?.Session);
        Assert.Null(host.Terminal);
    }

    [Fact, Trait("Lane", "unit")]
    public void HostRules_uses_the_agents_TMUX_only_when_it_names_this_server()
    {
        var table = TmuxTree();

        var named = Detect(new FakeHostSources(), table[2], table, ("TMUX", Socket + ",400,0"));
        var other = Detect(new FakeHostSources(), table[2], table, ("TMUX", Socket + ",999,0"));

        Assert.Equal(Socket, named.Tmux?.Socket);
        Assert.Null(other.Tmux?.Socket);
    }

    [Fact, Trait("Lane", "unit")]
    public void HostRules_finds_warp_kitty_and_ssh()
    {
        var warp = new[] { Proc(200, 1, "warp-terminal"), Proc(210, 200, "zsh"), Proc(211, 210, "claude") };
        var warpSources = new FakeHostSources();
        warpSources.Environments[210] = Env(("TERM_PROGRAM", "WarpTerminal"), ("WARP_FOCUS_URL", "warp://session/0123456789abcdef0123456789abcdef"));
        var kitty = new[] { Proc(300, 1, "kitty"), Proc(310, 300, "bash"), Proc(311, 310, "claude") };
        var kittySources = new FakeHostSources();
        kittySources.Environments[310] = Env(("KITTY_WINDOW_ID", "7"), ("WINDOWID", "5"));
        var ssh = new[] { Proc(500, 1, "sshd"), Proc(510, 500, "sshd"), Proc(521, 510, "claude") };

        var inWarp = Detect(warpSources, warp[2], warp);
        var inKitty = Detect(kittySources, kitty[2], kitty);
        var overSsh = Detect(new FakeHostSources(), ssh[2], ssh);

        Assert.Equal(("warp", "Warp", "warp://session/0123456789abcdef0123456789abcdef"), (inWarp.Kind, inWarp.Label, inWarp.Terminal?.WarpFocusUrl));
        Assert.Equal(("terminal", "kitty", "7", "5"), (inKitty.Kind, inKitty.Label, inKitty.Terminal?.KittyWindow, inKitty.Terminal?.XWindow));
        Assert.Equal(("ssh", "ssh"), (overSsh.Kind, overSsh.Label));
        Assert.Null(overSsh.Terminal);   // spec §6.1: ssh shows no terminal detail
    }

    [Fact, Trait("Lane", "unit")]
    public void HostRules_refuses_a_warp_url_that_is_not_a_session_url()
    {
        var warp = new[] { Proc(200, 1, "warp-terminal"), Proc(210, 200, "zsh"), Proc(211, 210, "claude") };
        var sources = new FakeHostSources();
        sources.Environments[210] = Env(("WARP_FOCUS_URL", "warp://session/../etc"));

        var host = Detect(sources, warp[2], warp);

        Assert.Null(host.Terminal?.WarpFocusUrl);
    }

    [Fact, Trait("Lane", "unit")]
    public void HostRules_falls_back_to_the_agents_warp_variable_then_t3_then_unknown()
    {
        var lone = Proc(1000, 1, "claude");

        var warp = Detect(new FakeHostSources(), lone, [lone], ("TERM_PROGRAM", "WarpTerminal"));
        var t3 = HostRules.Detect(new HostQuery(new Dictionary<int, ProcessEntry> { [1000] = lone }, lone, Env()) { T3 = true }, new FakeHostSources());
        var none = Detect(new FakeHostSources(), lone, [lone]);

        Assert.Equal(("warp", "t3", "unknown"), (warp.Kind, t3.Kind, none.Kind));
    }

    [Fact, Trait("Lane", "unit")]
    public void HostRules_marks_a_team_socket()
    {
        var teams = new[] { new TeamNames("t1", ["api", "web"]) };

        Assert.Equal("sindri", HostRules.SocketKind("/run/x/sindri", "any", teams));
        Assert.Equal("sindri", HostRules.SocketKind("/tmp/tmux-1000/sindri-launch", "snd-LCH-1", teams));   // a launched session is Sindri's, not 'other'
        Assert.Equal("default", HostRules.SocketKind("/tmp/tmux-1000/default", "t1-api", teams));   // the owner's lanes on the default socket stay 'default'
        Assert.Equal("team", HostRules.SocketKind("/tmp/tmux-1000/t1", "x", teams));   // a socket named for a team
        Assert.Equal("team", HostRules.SocketKind("/tmp/other", "t1-web", teams));   // a session named <team>-<lane>
        Assert.Equal("other", HostRules.SocketKind("/tmp/other", "t1-db", teams));
        Assert.Equal("other", HostRules.SocketKind(socket: null, session: null, teams: teams));
    }

    [Fact, Trait("Lane", "unit")]
    public void HostChain_stops_at_a_cycle_a_missing_parent_and_the_step_limit()
    {
        var a = Proc(10, 11, "a");
        var b = Proc(11, 10, "b");
        var long_ = Enumerable.Range(1, 40).Select(i => Proc(pid: i, parent: i + 1, comm: "x")).ToDictionary(static p => p.Pid);

        Assert.Equal([11], HostChain.Ancestors(new Dictionary<int, ProcessEntry> { [10] = a, [11] = b }, a).Select(static p => p.Pid));
        Assert.Empty(HostChain.Ancestors(new Dictionary<int, ProcessEntry> { [10] = a }, a));
        Assert.Equal(HostChain.MaxSteps, HostChain.Ancestors(long_, long_[1]).Count);
    }
}
