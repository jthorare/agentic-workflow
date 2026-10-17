using System.Text;
using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Kernel.Services;
using Aw.Sindri.Features.OnyxDown;
using Aw.Sindri.Features.OnyxUp;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Host.Onyx;
using Aw.Sindri.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Commands;

/// <summary>'sindri onyx up' and 'onyx down' as OnyxVerbs runs them, over the fake rig: who may run them, what they print where, and the exit codes.</summary>
[Collection(ProcessEnvironment.Name)]
public sealed class OnyxUpCommandTests : IDisposable
{
    private const string HandOver = "Onyx is running. Account setup (sign-up and Sindri's key) comes next: `sindri onyx up` will continue it once that stage lands.";

    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(SindriJsonContext.Default);

    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly OnyxUpRig _rig = new();
    private readonly ServiceProvider _services;

    public OnyxUpCommandTests()
    {
        _rig.Progress.Forward = new ConsoleOnyxProgress(_error);
        _services = new ServiceCollection()
            .AddSingleton(_rig.Service())
            .AddSingleton<Core.Onyx.IOnyxProgress>(_rig.Progress)
            .AddSingleton<IService<OnyxDownRequest, OnyxDownResponse>>(
                new OnyxDownService(_rig.Workspace, new Core.Onyx.OnyxFacts(_rig.Runtime, _rig.Probes, new FakeSindriHome(OnyxUpRig.Home)), _rig.Runtime))
            .BuildServiceProvider();
    }

    public void Dispose() => _services.Dispose();

    private static AgentSession Human { get; } = new(Signal: null, Terminal: true, Prover: false);

    private OnyxVerbs Verbs(AgentSession session) => new(_services, new Output(_out, _error, _json), session);

    [Fact, Trait("Lane", "unit")]
    public async Task Up_as_a_session_is_refused_with_SND_AUTH_002_and_calls_nothing()
    {
        var code = await Verbs(new AgentSession("CLAUDECODE is set", Terminal: true, Prover: false)).Up(json: false, TestContext.Current.CancellationToken);

        Assert.Equal(1, code);
        Assert.Contains("SND-AUTH-002", _error.ToString(), StringComparison.Ordinal);
        Assert.Contains("CLAUDECODE is set", _error.ToString(), StringComparison.Ordinal);
        Assert.Contains("ask the human to run `sindri onyx up`", _error.ToString(), StringComparison.Ordinal);
        Assert.Empty(_rig.Runtime.Calls);
        Assert.Equal(string.Empty, _out.ToString());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Up_without_a_terminal_is_refused()
    {
        var code = await Verbs(new AgentSession(Signal: null, Terminal: false, Prover: false)).Up(json: false, TestContext.Current.CancellationToken);

        Assert.Equal(1, code);
        Assert.Contains("SND-AUTH-002", _error.ToString(), StringComparison.Ordinal);
        Assert.Contains("no terminal", _error.ToString(), StringComparison.Ordinal);
        Assert.Empty(_rig.Runtime.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Up_prints_url_version_and_the_P3_hand_over_line_on_stdout_and_progress_on_stderr()
    {
        var code = await Verbs(Human).Up(json: false, TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        var lines = _out.ToString().TrimEnd('\n').Split('\n');
        Assert.Equal(
            [
                "url       http://127.0.0.1:3000",
                "version   v4.9.0",
                "ran       fetch, config, pull, start",
                "skipped   none",
                HandOver,
            ],
            lines,
            StringComparer.Ordinal);
        Assert.Contains("fetch: running\n", _error.ToString().Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains("start: running", _error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("running\n", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Up_names_the_skipped_steps_on_a_rerun()
    {
        _ = await Verbs(Human).Up(json: false, TestContext.Current.CancellationToken);
        _out.GetStringBuilder().Clear();

        _ = await Verbs(Human).Up(json: false, TestContext.Current.CancellationToken);

        Assert.Contains("ran       none", _out.ToString(), StringComparison.Ordinal);
        Assert.Contains("skipped   fetch, config, pull, start", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Up_json_prints_the_response_only()
    {
        var code = await Verbs(Human).Up(json: true, TestContext.Current.CancellationToken);

        var response = AwJson.Deserialize<OnyxUpResponse>(Encoding.UTF8.GetBytes(_out.ToString()), _json).Value;
        Assert.Equal(0, code);
        Assert.Equal("http://127.0.0.1:3000", response.Url);
        Assert.DoesNotContain("USER_AUTH", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Up_json_documents_steps_as_an_array_of_step_and_outcome()
    {
        _ = await Verbs(Human).Up(json: true, TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(_out.ToString());
        var steps = document.RootElement.GetProperty("steps").EnumerateArray().Select(static e => (e.GetProperty("step").GetString(), e.GetProperty("outcome").GetString())).ToArray();
        Assert.Equal([("fetch", "ran"), ("config", "ran"), ("pull", "ran"), ("start", "ran")], steps);
        Assert.False(document.RootElement.TryGetProperty("ran", out _));
        Assert.False(document.RootElement.TryGetProperty("skipped", out _));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_hand_over_line_follows_the_response()
    {
        var needsYou = new OnyxUpResponse(3000, "http://127.0.0.1:3000", "v4.9.0", [], [new OnyxStepResult("fetch", OnyxStepOutcome.Ran), new OnyxStepResult("account", OnyxStepOutcome.NeedsHuman)]);
        await using var provider = new ServiceCollection().AddSingleton<IService<OnyxUpRequest, OnyxUpResponse>>(new StubUp(needsYou)).BuildServiceProvider();

        var code = await new OnyxVerbs(provider, new Output(_out, _error, _json), Human).Up(json: false, TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        Assert.Contains("needs you: account", _out.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(HandOver, _out.ToString(), StringComparison.Ordinal);
    }

    private sealed class StubUp(OnyxUpResponse response) : IService<OnyxUpRequest, OnyxUpResponse>
    {
        public ValueTask<Kernel.Result<OnyxUpResponse>> Handle(OnyxUpRequest request, CancellationToken cancellationToken) => ValueTask.FromResult<Kernel.Result<OnyxUpResponse>>(response);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Up_maps_SND_ONYX_003_to_exit_1_with_the_fix_text()
    {
        _rig.Probes.FreeBytes = 1L << 30;

        var code = await Verbs(Human).Up(json: false, TestContext.Current.CancellationToken);

        Assert.Equal(1, code);
        Assert.Contains("SND-ONYX-003", _error.ToString(), StringComparison.Ordinal);
        Assert.Contains("Free space on that volume, then run this again", _error.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, _out.ToString());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Up_maps_a_busy_lock_to_exit_1()
    {
        using var held = _rig.Workspace.TryLock().Value;

        var code = await Verbs(Human).Up(json: false, TestContext.Current.CancellationToken);

        Assert.Equal(1, code);
        Assert.Contains("SND-API-009", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Up_interrupted_mid_pull_exits_130_with_the_resume_line_and_releases_the_lock()
    {
        using var source = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        _rig.Runtime.OnCall = operation =>
        {
            if (operation == "Pull")
            {
                source.Cancel();
            }
        };

        var code = await Verbs(Human).Up(json: false, source.Token);

        Assert.Equal(130, code);
        Assert.Contains("interrupted; run `sindri onyx up` again to resume", _error.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, _out.ToString());
        Assert.False(_rig.Workspace.LockHeld);
        Assert.DoesNotContain("Up", _rig.Runtime.Calls, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Up_interrupted_in_the_middle_of_a_dot_row_ends_the_row_before_the_resume_line()
    {
        using var source = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        _rig.Runtime.OnCall = operation =>
        {
            if (operation == "Pull")
            {
                _rig.Progress.Tick();
                _rig.Progress.Tick();
                _rig.Progress.Tick();
                source.Cancel();
            }
        };

        _ = await Verbs(Human).Up(json: false, source.Token);

        var error = _error.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Contains("...\ninterrupted; run `sindri onyx up` again to resume\n", error, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Down_interrupted_exits_130_and_releases_the_lock()
    {
        _ = await Verbs(Human).Up(json: false, TestContext.Current.CancellationToken);
        using var source = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        _rig.Runtime.OnCall = operation =>
        {
            if (operation == "Stop")
            {
                source.Cancel();
            }
        };

        var code = await Verbs(Human).Down(json: true, source.Token);

        Assert.Equal(130, code);
        Assert.Contains("interrupted", _error.ToString(), StringComparison.Ordinal);
        Assert.False(_rig.Workspace.LockHeld);
    }

    [Fact, Trait("Lane", "unit")]
    public void Ctrl_C_and_SIGTERM_are_listened_for_during_onyx_up_and_down_only()
    {
        Assert.True(Interrupt.Streams(["onyx", "up"]));
        Assert.True(Interrupt.Streams(["onyx", "up", "--json"]));
        Assert.True(Interrupt.Streams(["onyx", "down"]));
        Assert.False(Interrupt.Streams(["onyx", "status"]));
        Assert.False(Interrupt.Streams(["onyx"]));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Down_as_a_session_is_refused()
    {
        var code = await Verbs(new AgentSession("CLAUDECODE is set", Terminal: true, Prover: false)).Down(json: false, TestContext.Current.CancellationToken);

        Assert.Equal(1, code);
        Assert.Contains("SND-AUTH-002", _error.ToString(), StringComparison.Ordinal);
        Assert.Empty(_rig.Runtime.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Down_prints_stopped_and_exits_0()
    {
        _ = await Verbs(Human).Up(json: false, TestContext.Current.CancellationToken);
        _out.GetStringBuilder().Clear();

        var code = await Verbs(Human).Down(json: false, TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        Assert.Contains("Onyx stopped", _out.ToString(), StringComparison.Ordinal);
        Assert.All(_rig.Runtime.Containers, static c => Assert.Equal("exited", c.State, StringComparer.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Down_on_a_box_never_set_up_says_so_and_exits_0()
    {
        var code = await Verbs(Human).Down(json: false, TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        Assert.Contains("stopped", _out.ToString(), StringComparison.Ordinal);
        Assert.Contains("nothing was running", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Down_maps_a_remote_docker_to_exit_1()
    {
        _ = await Verbs(Human).Up(json: false, TestContext.Current.CancellationToken);
        _rig.Runtime.Facts = FakeContainerRuntime.Healthy with { Endpoint = "tcp://10.0.0.5:2375" };

        var code = await Verbs(Human).Down(json: false, TestContext.Current.CancellationToken);

        Assert.Equal(1, code);
        Assert.Contains("SND-ONYX-010", _error.ToString(), StringComparison.Ordinal);
    }
}
