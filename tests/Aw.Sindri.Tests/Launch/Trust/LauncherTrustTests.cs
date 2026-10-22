using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Launch.Trust;

/// <summary>The launcher pre-trusts a claude-code Step's worktree before the session starts, and ends its sessions when serve stops.</summary>
[Lane(TestLane.Unit)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class LauncherTrustTests : IDisposable
{
    private readonly string _home = Directory.CreateTempSubdirectory("aw-launcher-trust-").FullName;
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly FakeSessionHost _host = new();
    private readonly RecordingTrust _trust = new();
    private readonly List<Core.Workflow.Engine.LaunchId> _ended = [];

    public void Dispose() => Directory.Delete(_home, recursive: true);

    private Launcher Make(params AgentEntry[] entries) =>
        new(new LauncherParts(new AgentCatalog(entries.Length == 0 ? [AgentEntries.ClaudeCode, AgentEntries.Cursor] : entries), _ => _host, new FakeTerminalViewer(), new LauncherOptions(_home), _clock)
        {
            ResolveBin = static (_, _) => ValueTask.FromResult<string?>(null),
            Trust = _trust,
            EndLive = (launch, _) =>
            {
                _ended.Add(launch);
                return ValueTask.CompletedTask;
            },
        });

    private LaunchRequest Step(string agent = "claude-code") =>
        A.LaunchRequest().WithAgent(AgentId.From(agent)).Unattended(new StepArgs(Model: null, Policy: null, new StepRef("build", "agent"))).Build() with { Worktree = _home, RepoPath = _home };

    [Fact]
    public async Task A_claude_code_step_trusts_its_worktree_before_the_session_starts()
    {
        _trust.OnTrust = () => Expect.Equal(0, _host.Started.Count);

        var result = await Make().Launch(Step(), CancellationToken.None);

        Expect.True(result is StartedLaunch);
        Expect.Sequence([_home], _trust.Trusted);
    }

    [Fact]
    public async Task A_refused_trust_fails_the_launch_without_starting_a_session()
    {
        _trust.Failure = SindriErrors.LaunchTrustUnavailable.With("repair ~/.claude.json");

        var result = await Make().Launch(Step(), CancellationToken.None);

        Expect.True(result is FailedLaunch failed && failed.Error.Code == SindriErrors.LaunchTrustUnavailable.Code);
        Expect.Equal(0, _host.Started.Count);
    }

    [Fact]
    public async Task Another_agent_and_a_human_open_trust_nothing()
    {
        _ = await Make().Launch(Step("cursor"), CancellationToken.None);
        _ = await Make().Launch(A.LaunchRequest().Build(), CancellationToken.None);

        Expect.Equal(0, _trust.Trusted.Count);
    }

    [Fact]
    public async Task Stop_all_ends_every_tmux_session_it_launched_and_records_each()
    {
        var launcher = Make();
        var one = Step() with { Launch = Core.Workflow.Engine.LaunchId.For(new Guid(1, 0, 0, new byte[8])) };
        var two = Step() with { Launch = Core.Workflow.Engine.LaunchId.For(new Guid(2, 0, 0, new byte[8])) };
        _ = await launcher.Launch(one, CancellationToken.None);
        _ = await launcher.Launch(two, CancellationToken.None);

        var count = await launcher.StopAll(CancellationToken.None);

        Expect.Equal(2, count);
        Expect.Equal(0, _host.LiveCount);
        Expect.Equal(2, _ended.Count);
    }

    [Fact]
    public async Task Stop_all_with_nothing_launched_is_zero() =>
        Expect.Equal(0, await Make().StopAll(CancellationToken.None));

    private sealed class RecordingTrust : IAgentTrust
    {
        public List<string> Trusted { get; } = [];

        public Error? Failure { get; set; }

        public Action? OnTrust { get; set; }

        public ValueTask<Result<Unit>> Trust(string directory, CancellationToken cancellationToken)
        {
            OnTrust?.Invoke();
            Trusted.Add(directory);
            return ValueTask.FromResult(Failure is { } failure ? failure : Results.Ok());
        }

        public ValueTask<Result<Unit>> Untrust(string directory, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Ok());
    }
}
