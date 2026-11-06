using Aw.Meta.Core.Config;
using Aw.Meta.Infrastructure.Processes;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Adapters;

public sealed class TmuxLanePaneTests
{
    private readonly InMemoryCommandRunner _runner = new();

    private readonly InMemoryDelay _delay = new(new ManualClock(A.T0));

    private readonly InMemoryMetaConfig _config = new();

    private TmuxLanePane Pane() => new(_runner, "tmux", socket: null, _config, _delay);

    private async Task<TmuxLanePane> TeamPane()
    {
        _ = await _config.Save(new TeamConfig("t1", "/work", ["meta", "api"], "meta"), CancellationToken.None);
        return Pane();
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_probe_reads_the_pane_command_and_counts_attached_clients_on_the_exact_session()
    {
        _ = _runner.When("display-message", "codex\n").When("list-clients", "x\nx\n");

        var state = await (await TeamPane()).Probe("t1-api", CancellationToken.None);

        Assert.Equal(("codex", 2), (state.Value!.Command, state.Value.Clients));
        Assert.Contains(_runner.Lines, l => string.Equals(l, "tmux -L t1 display-message -p -t =t1-api: #{pane_current_command}", StringComparison.Ordinal));
        Assert.Contains(_runner.Lines, l => string.Equals(l, "tmux -L t1 list-clients -t =t1-api -F x", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_missing_session_probes_as_null()
    {
        _ = _runner.When("display-message", exitCode: 1);

        Assert.Null((await (await TeamPane()).Probe("t1-api", CancellationToken.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_wake_types_the_line_literally_to_the_exact_session_then_presses_Enter()
    {
        _ = _runner.When("send-keys");

        var result = await (await TeamPane()).Wake("t1-api", "hello -x; $(date)", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["tmux -L t1 send-keys -t =t1-api: -l hello -x; $(date)", "tmux -L t1 send-keys -t =t1-api: Enter"], _runner.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_pane_pid_is_read_with_the_exact_session_on_the_team_socket()
    {
        _ = _runner.When("display-message", "4242\n");

        var pid = await (await TeamPane()).PanePid("t1-api", CancellationToken.None);

        Assert.Equal(4242, pid.Value);
        Assert.Equal(["tmux -L t1 display-message -p -t =t1-api: #{pane_pid}"], _runner.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Type_sends_the_text_literally_without_Enter_and_PressEnter_sends_only_Enter()
    {
        _ = _runner.When("send-keys");
        var pane = await TeamPane();

        var typed = await pane.Type("t1-api", "Read and continue from /x -y; $(date)", CancellationToken.None);
        var entered = await pane.PressEnter("t1-api", CancellationToken.None);

        Assert.True(typed.IsSuccess && entered.IsSuccess);
        Assert.Equal(["tmux -L t1 send-keys -t =t1-api: -l Read and continue from /x -y; $(date)", "tmux -L t1 send-keys -t =t1-api: Enter"], _runner.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Capture_reads_the_visible_pane_of_the_exact_session()
    {
        _ = _runner.When("capture-pane", "screen\n");

        var shown = await (await TeamPane()).Capture("t1-api", CancellationToken.None);

        Assert.Equal("screen\n", shown.Value);
        Assert.Equal(["tmux -L t1 capture-pane -p -t =t1-api:"], _runner.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failing_type_press_or_capture_is_an_error_naming_the_exit()
    {
        _ = _runner.When("send-keys", exitCode: 1).When("capture-pane", exitCode: 1);
        var pane = await TeamPane();

        Assert.True((await pane.Type("t1-api", "x", CancellationToken.None)).IsError);
        Assert.True((await pane.PressEnter("t1-api", CancellationToken.None)).IsError);
        Assert.True((await pane.Capture("t1-api", CancellationToken.None)).IsError);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_send_keys_is_an_error_and_Enter_is_not_sent()
    {
        _ = _runner.When("send-keys", exitCode: 1);

        var result = await (await TeamPane()).Wake("t1-api", "hi", CancellationToken.None);

        Assert.True(result.IsError);
        _ = Assert.Single(_runner.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Every_tmux_call_names_the_socket_the_override_gives_and_without_one_the_team_name()
    {
        _ = _runner.When("display-message", "codex\n").When("list-clients").When("send-keys");
        var withSocket = new TmuxLanePane(_runner, "tmux", "t1-socket", _config, _delay);

        _ = await withSocket.Probe("t1-api", CancellationToken.None);
        _ = await withSocket.Wake("t1-api", "hi", CancellationToken.None);
        var team = new InMemoryCommandRunner().When("display-message", "codex\n").When("list-clients");
        _ = await _config.Save(new TeamConfig("t1", "/work", ["meta", "api"], "meta"), CancellationToken.None);
        var plain = new TmuxLanePane(team, "tmux", socket: null, _config, _delay);
        _ = await plain.Probe("t1-api", CancellationToken.None);

        Assert.All(_runner.Lines, l => Assert.StartsWith("tmux -L t1-socket ", l, StringComparison.Ordinal));
        Assert.Equal(4, _runner.Lines.Count);
        Assert.All(team.Lines, l => Assert.StartsWith("tmux -L t1 ", l, StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_team_config_and_a_socket_the_lane_is_down_and_a_wake_is_an_error_and_no_tmux_runs()
    {
        var pane = Pane();

        Assert.Null((await pane.Probe("t1-api", CancellationToken.None)).Value);
        Assert.True((await pane.Wake("t1-api", "hi", CancellationToken.None)).IsError);
        Assert.Empty(_runner.Lines);
    }
}
