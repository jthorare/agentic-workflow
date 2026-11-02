using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;
using Aw.Sindri.Testing.Sandbox;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Launch;

/// <summary>
/// SB-5a in-process proof, written against the seam (<see cref="LauncherParts.Sandbox"/>): the real <see cref="Launcher"/>, a PassThroughSandbox behind a test
/// <see cref="ISandboxLaunch"/>, and a stub agent on a private tmux server (never the box's default one). An unattended Step goes through the sandbox step,
/// the wrapped argv runs, 'launch.started' carries the plan's summary, and Stop releases the launch.
/// </summary>
[Lane(TestLane.Integration)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class SandboxStepProofTests : IDisposable
{
    private readonly PrivateTmux _tmux = new();
    private readonly string _home = Directory.CreateTempSubdirectory("aw-sandbox-proof-").FullName;
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly RecordingSandbox _sandbox = new(new PassThroughSandbox());
    private readonly PassThroughSandboxLaunch _launch;
    private readonly string _agent;

    public SandboxStepProofTests()
    {
        _launch = new PassThroughSandboxLaunch(_sandbox);
        _agent = Path.Combine(_tmux.Scratch, "agent.sh");   // the stub agent: stays alive like 'sindri session wrap'; written before any process of this class starts
        _ = ExecutableScript.Write(_agent, "sleep 60\n");
    }

    public void Dispose()
    {
        _tmux.Dispose();
        Directory.Delete(_home, recursive: true);
    }

    private Launcher Make() =>
        new(new LauncherParts(new AgentCatalog([AgentEntries.ClaudeCode]), _ => new TmuxSessionHost(_tmux.Options), new FakeTerminalViewer(), new LauncherOptions(_home) { SindriExecutable = _agent }, _clock)
        {
            ResolveBin = static (_, _) => ValueTask.FromResult<string?>(null),
            Sandbox = _launch,   // MARK(SB-5a switch): flow replaces this test adapter with the real one in LauncherServiceCollectionExtensions
        });

    [Fact]
    public async Task An_unattended_Step_runs_through_the_sandbox_step_and_Stop_releases_it()
    {
        var launcher = Make();
        var request = A.LaunchRequest().InDirectory(_home).Unattended(new StepArgs(Model: null, new StepPolicy([], StepPermissionMode.ReadOnly, WritablePath: null), new StepRef("build", "agent"))).Build();

        var result = await launcher.Launch(request, TestContext.Current.CancellationToken);

        var started = Assert.IsType<StartedLaunch>(result);
        _ = Assert.Single(_sandbox.Wrapped);
        Assert.Equal(1, _launch.Prepared);
        Assert.NotNull(started.Sandbox);
        Assert.True(_launch.Chosen is not null);   // claude-code pre-chooses its session id: the sandbox step gets it
        Assert.Equal(started.ChosenSession, _launch.Chosen?.Value);
        Assert.True(_tmux.HasSession(started.Host.Name));

        var stopped = await launcher.Stop(request.Launch, TestContext.Current.CancellationToken);

        Assert.True(stopped.IsSuccess);
        Assert.False(_tmux.HasSession(started.Host.Name));
        Assert.Equal([request.Launch], _launch.Released);
    }

    /// <summary>What the flow's adapter does with a sandbox, minus credentials, egress and the session home: plan, self-check, wrap.</summary>
    private sealed class PassThroughSandboxLaunch(ISandbox sandbox) : ISandboxLaunch
    {
        public int Prepared { get; private set; }

        public SessionId? Chosen { get; private set; }

        public List<LaunchId> Released { get; } = [];

        public async ValueTask<Result<PreparedSandbox>> Prepare(LaunchRequest request, IReadOnlyList<string> argv, SessionId? chosenSession, CancellationToken cancellationToken)
        {
            Chosen = chosenSession;
            var plan = sandbox.Plan(SandboxRequests.ReadOnly with { Launch = request.Launch });
            if (plan.IsError)
            {
                return plan.Error;
            }

            var self = await sandbox.SelfCheck(plan.Value, cancellationToken);
            if (self.IsError)
            {
                return self.Error;
            }

            var wrapped = sandbox.Wrap(plan.Value, argv);
            Prepared++;
            return wrapped.IsError ? wrapped.Error : new PreparedSandbox(plan.Value.ToSummary(), wrapped.Value, "/unused/session-home", SandboxRequests.RunArea);
        }

        public ValueTask Release(LaunchId launch, CancellationToken cancellationToken)
        {
            Released.Add(launch);
            return ValueTask.CompletedTask;
        }
    }
}
