using Aw.Kernel.Testing;
using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Ports;
using Aw.Meta.Infrastructure.Processes;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Ports;

/// <summary>TmuxLanePane over a scripted command runner that answers the way tmux does: no real tmux, no AW_TMUX program is ever started.</summary>
[Lane(TestLane.Unit)]
public sealed class TmuxLanePaneContractTests : LanePaneContract
{
    protected override async ValueTask<(ILanePane Pane, Action<string, string, int> Start)> Create()
    {
        var sessions = new Dictionary<string, (string Command, int Clients)>(StringComparer.Ordinal);
        var config = new InMemoryMetaConfig();
        _ = await config.Save(new TeamConfig("t1", "/work", ["meta", "api"], "meta"), CancellationToken.None);

        // "-t =name:" or "-t =name": tmux resolves an exact session name; anything else is "can't find".
        static string? Session(CommandRequest request)
        {
            var at = request.Argv.ToList().IndexOf("-t");
            return at < 0 || at + 1 >= request.Argv.Count ? null : request.Argv[at + 1].TrimStart('=').TrimEnd(':');
        }

        static bool Live(CommandRequest request, Dictionary<string, (string Command, int Clients)> all) => Session(request) is { } s && all.ContainsKey(s);

        var runner = new InMemoryCommandRunner()
            .When("display-message", r => Session(r) is { } s && sessions.TryGetValue(s, out var found) ? Ok(Shown(r, found.Command)) : Failed())
            .When("capture-pane", r => Live(r, sessions) ? Ok("output\n") : Failed())
            .When("list-clients", r => Session(r) is { } s && sessions.TryGetValue(s, out var found) ? Ok(string.Concat(Enumerable.Repeat("x\n", found.Clients))) : Failed())
            .When("send-keys", r => Live(r, sessions) ? Ok(string.Empty) : Failed());
        return (new TmuxLanePane(runner, "tmux-never-started", "t1", config, new InMemoryDelay(new ManualClock(A.T0))), (session, command, clients) => sessions[session] = (command, clients));
    }

    // display-message answers the pane's pid for #{pane_pid} and the command otherwise.
    private static string Shown(CommandRequest request, string command) => (string.Equals(request.Argv[^1], "#{pane_pid}", StringComparison.Ordinal) ? "4242" : command) + "\n";

    private static CommandResult Ok(string stdout) => new(0, stdout, string.Empty, TimedOut: false);

    private static CommandResult Failed() => new(1, string.Empty, "can't find session", TimedOut: false);
}
