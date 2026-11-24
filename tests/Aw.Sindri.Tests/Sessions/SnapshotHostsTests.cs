using System.Runtime.Versioning;
using System.Text;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Features.Sessions;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Infrastructure.Sessions;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>The hosts of the fixture tree's sessions (collector spec CO-2b): real /proc and /proc/net/unix adapters, the real terminals.json, a fake tmux server and one Meta team.</summary>
[UnsupportedOSPlatform("windows")]
public sealed class SnapshotHostsTests
{
    private const string Socket = "/tmp/tmux-1000/t1";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<SessionSnapshot> Discover(FakeTmuxServers tmux)
    {
        var clock = new FakeTimeProvider(KnownInstants.Epoch);
        using var fleet = new TempFleet();
        var catalog = new AgentCatalog(LauncherConfigFile.Read(SessionFixtures.Builtins(), """{ "schema": 1 }"""u8.ToArray()).Value.Agents);
        var providers = catalog.All.Select(static a => a.Id).ToList();
        var table = new ProcProcessTable(SessionFixtures.Proc, SessionFixtures.OwnUid);
        var hosts = new SessionHosts(table, new UnixSockets(SessionFixtures.Proc), tmux, new FakeTeamNames(new TeamNames("t1", ["api"])), TerminalProcessNames.Embedded(), new FakeLaunchFiles());
        var discovery = new SessionDiscovery(table, new ClaudeSessionRecords(SessionFixtures.ClaudeSessions),
            new FleetStateFiles(fleet.Root, providers, pruning: false), catalog, SessionSettings.Defaults, hosts, clock);
        var observer = new SessionObserver(new TranscriptFiles("/nonexistent-fixture-root/home"), new PassThroughScrubber(), new SessionRepos(new FakeRepoLocator(), new InMemoryProfiles(), clock));
        var snapshot = new SessionSnapshotStore();
        var service = new CollectSessionsService(discovery, observer, new CollectorMemory(), snapshot, clock, Microsoft.Extensions.Logging.Abstractions.NullLogger<CollectSessionsService>.Instance);

        _ = await service.Handle(new CollectSessionsRequest(TimeSpan.FromSeconds(30)), Token);

        return snapshot.Current!;
    }

    private static FakeTmuxServers OneServer() => new FakeTmuxServers().With(Socket,
        new TmuxServerView([new TmuxPane(410, "api", 0, 0, "%0")], [new TmuxClient(301, "/dev/pts/1", "api")]));

    private static GoldenHost Row(ObservedSession s) => new(s.Ref.ToString(), s.Host.Kind, s.Host.Label, s.Host.Tmux?.Socket, s.Host.Tmux?.SocketKind, s.Host.Tmux?.Session, s.Host.Tmux?.Pane,
        s.Host.Terminal?.Name, s.Host.Terminal?.WarpFocusUrl ?? s.Host.Terminal?.KittyWindow);

    private static string Json(SessionSnapshot snapshot) =>
        Encoding.UTF8.GetString(AwJson.Serialize(new GoldenHosts([.. snapshot.Sessions.Select(Row)]), AwJson.CreateOptions(SessionGoldenJsonContext.Default, indented: true))) + "\n";

    [Fact, Trait("Lane", "integration")]
    public async Task The_hosts_over_the_fixture_tree_equal_the_golden()
    {
        var snapshot = await Discover(OneServer());

        Golden.Match(Json(snapshot), typeof(SnapshotHostsTests).Assembly, "Sessions/Golden/snapshot-hosts.json", Normalization.None);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_unreachable_tmux_server_leaves_tmux_with_no_place_and_no_problem_row()
    {
        var snapshot = await Discover(new FakeTmuxServers());

        var tmux = snapshot.Sessions.Single(static s => s.Host.Kind == "tmux");

        Assert.Equal(("tmux", Socket, null), (tmux.Host.Label, tmux.Host.Tmux?.Socket, tmux.Host.Tmux?.Session));
        Assert.DoesNotContain(snapshot.Problems, static p => p.Source == "tmux");
    }
}
