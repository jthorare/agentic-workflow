using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Redaction;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Toolkits;
using Aw.Sindri.Infrastructure.Text;
using Aw.Sindri.Infrastructure.Toolkits;
using Aw.Sindri.Testing.Toolkits;
using Aw.Sindri.Tests.BuildToolkit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Toolkits;

/// <summary>The real toolkit runtime (agent-first PR 16): it lists what the supervisor runs, calls through the process and logs each call.</summary>
[Lane(TestLane.Unit)]
public sealed class RunningToolkitsTests : IAsyncDisposable
{
    private static readonly Toolkit _triage = new("triage", "/tk/triage", ToolkitState.Built, SourceHash: "h", BuiltAt: DateTimeOffset.UnixEpoch, ToolkitManifest.Parse(ToolkitRig.Manifest).Value, CreatedBy: "human");

    private readonly FakeTimeProvider _clock = new();
    private readonly ToolkitRig.FakeStore _store = new();
    private readonly InMemoryToolkitCalls _calls = new();
    private readonly ScriptedLauncher _launcher;
    private readonly ToolkitSupervisor _supervisor;
    private readonly RunningToolkits _runtime;

    public RunningToolkitsTests()
    {
        _store.Items["triage"] = _triage;
        _launcher = new ScriptedLauncher(_clock);
        _supervisor = new ToolkitSupervisor(_launcher, _store, _clock);
        _runtime = new RunningToolkits(_supervisor, _store, new ToolkitCallLog(_calls, new RedactingScrubber(Redactor.Default)), _clock);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public ValueTask DisposeAsync() => _supervisor.DisposeAsync();

    [Fact]
    public async Task A_started_toolkit_is_listed_running_with_its_manifest_and_a_stopped_one_is_not()
    {
        Assert.Empty(_runtime.Toolkits());

        _ = await _supervisor.Start(_triage, Token);
        var listed = Assert.Single(_runtime.Toolkits());
        Assert.Equal(("triage", ToolkitState.Running), (listed.Name, listed.State));
        Assert.Equal("ping", Assert.Single(listed.Manifest!.Tools).Name);

        _ = await _supervisor.Stop("triage", Token);
        Assert.Empty(_runtime.Toolkits());
    }

    [Fact]
    public async Task A_call_goes_to_the_process_and_is_logged_with_caller_duration_input_and_output()
    {
        _ = await _supervisor.Start(_triage, Token);
        _launcher.Last.Answer = _ => """{"pong":true}""";
        _launcher.Last.Takes = TimeSpan.FromMilliseconds(250);

        var answer = await _runtime.CallTool("triage", "ping", """{"n":1}""", "session:abc", Token);

        Assert.Equal("""{"pong":true}""", answer.Value);
        var row = Assert.Single(_calls.All);
        Assert.Equal(("triage", "ping", "session:abc", """{"n":1}""", """{"pong":true}""", null, 250L), (row.Toolkit, row.Tool, row.Caller, row.Input, row.Output, row.Error, row.DurationMs));
    }

    [Fact]
    public async Task A_paused_toolkits_refusal_names_a_tool_that_exists()
    {
        _store.Items["triage"] = _triage with { State = ToolkitState.Paused };

        var answer = await _runtime.CallTool("triage", "ping", "{}", "session:abc", Token);

        Assert.Equal(SindriErrors.ToolkitPaused.Code, answer.Error.Code);
        Assert.Contains("toolkit_stop", answer.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("sindri toolkit resume", answer.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failed_call_is_logged_with_its_error_and_returned()
    {
        _ = await _supervisor.Start(_triage, Token);
        _launcher.Last.Failure = SindriErrors.ToolkitToolFailed.With("boom");

        var answer = await _runtime.CallTool("triage", "ping", "{}", "workflow:run-1", Token);

        Assert.True(answer.IsError);
        var row = Assert.Single(_calls.All);
        Assert.Equal("workflow:run-1", row.Caller);
        Assert.Contains("boom", row.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_call_to_a_toolkit_that_is_not_running_says_why_and_is_not_logged()
    {
        var answer = await _runtime.CallTool("triage", "ping", "{}", "session:abc", Token);
        var unknown = await _runtime.CallTool("nope", "ping", "{}", "session:abc", Token);

        Assert.Equal("SND-TK-001", answer.Error.Code);   // built, not started
        Assert.Equal("SND-TK-009", unknown.Error.Code);
        Assert.Empty(_calls.All);
    }

    [Fact]
    public void The_infrastructure_registration_exposes_the_supervisor_as_the_running_toolkits()
    {
        var services = new ServiceCollection().AddToolkitInfrastructure()
            .AddSingleton<IToolkitStore>(_store)
            .AddSingleton<IToolkitProcessLauncher>(_launcher)
            .AddSingleton<TimeProvider>(_clock)
            .AddSingleton(new ToolkitCallLog(_calls, new RedactingScrubber(Redactor.Default)));
        using var provider = services.BuildServiceProvider();

        var runtime = provider.GetService<IRunningToolkits>();

        _ = Assert.IsType<RunningToolkits>(runtime);
    }

    private sealed class ScriptedLauncher(FakeTimeProvider clock) : IToolkitProcessLauncher
    {
        private readonly List<ScriptedProcess> _all = [];

        internal ScriptedProcess Last => _all[^1];

        public ValueTask<Result<IToolkitProcess>> Launch(Toolkit toolkit, CancellationToken cancellationToken)
        {
            var process = new ScriptedProcess(clock);
            _all.Add(process);
            return ValueTask.FromResult<Result<IToolkitProcess>>(process);
        }
    }

    private sealed class ScriptedProcess(FakeTimeProvider clock) : IToolkitProcess
    {
        private readonly TaskCompletionSource _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Exited => _exited.Task;

        internal Func<string, string> Answer { get; set; } = static _ => "{}";

        internal TimeSpan Takes { get; set; }

        internal Error? Failure { get; set; }

        public ValueTask<Result<string>> Call(string tool, string inputJson, TimeSpan timeout, CancellationToken cancellationToken)
        {
            clock.Advance(Takes);
            return ValueTask.FromResult(Failure is { } error ? (Result<string>)error : Answer(inputJson));
        }

        public ValueTask DisposeAsync()
        {
            _ = _exited.TrySetResult();
            return ValueTask.CompletedTask;
        }
    }
}
