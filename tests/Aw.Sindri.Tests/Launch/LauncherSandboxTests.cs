using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Launch;

/// <summary>The launcher's sandbox step (SB-5a): a Step runs the wrapped argv under the session home and says so on its start; a refusal starts nothing; the release follows the session.</summary>
[Lane(TestLane.Unit)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class LauncherSandboxTests : IDisposable
{
    private readonly string _home = Directory.CreateTempSubdirectory("aw-launcher-sandbox-").FullName;
    private readonly string _work = Directory.CreateTempSubdirectory("aw-launcher-sandbox-work-").FullName;
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly FakeSessionHost _host = new();
    private readonly FakeSandboxLaunch _sandbox = new();

    public void Dispose()
    {
        Directory.Delete(_home, recursive: true);
        Directory.Delete(_work, recursive: true);
    }

    private LauncherOptions Options => new(_home) { SindriHome = "/real/sindri-home" };

    private LaunchRequest Step() =>
        A.LaunchRequest().InDirectory(_work).Unattended(new StepArgs(Model: null, Policy: null, Ref: new StepRef("build", "agent"))).Build();

    private Launcher Make(ISandboxLaunch? sandbox) =>
        new(new LauncherParts(new AgentCatalog([AgentEntries.ClaudeCode]), _ => _host, new FakeTerminalViewer(), Options, _clock) { Sandbox = sandbox, ResolveBin = static (_, _) => ValueTask.FromResult<string?>(null) });

    [Fact]
    public async Task Without_a_sandbox_the_argv_runs_unchanged_and_no_summary_is_set()
    {
        var result = await Make(sandbox: null).Launch(Step(), CancellationToken.None);

        Expect.True(result is StartedLaunch { Sandbox: null });
        Expect.True(!_host.Started[0].Argv.Contains("fake-sandbox", StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_prepared_sandbox_runs_its_wrapped_argv_under_the_session_home_and_sets_the_summary()
    {
        var result = await Make(_sandbox).Launch(Step(), CancellationToken.None);

        Expect.True(result is StartedLaunch started && ReferenceEquals(started.Sandbox, _sandbox.Summary));
        var session = _host.Started[0];
        var at = session.Argv.ToList().FindIndex(a => string.Equals(a, "fake-sandbox", StringComparison.Ordinal));
        Expect.True(at > 0 && session.Argv[at + 1] == "--");   // 'sindri session wrap -- fake-sandbox -- <agent argv>'
        Expect.Equal("/sessions/home", session.Env["AW_SINDRI_HOME"]);
        Expect.Equal("/sessions/home/launch.secret", session.Env["AW_SINDRI_LAUNCH_SECRET_FILE"]);
        Expect.Equal(1, _sandbox.Prepared.Count);
        Expect.True(_sandbox.Prepared[0].Argv.SequenceEqual(session.Argv.Skip(at + 2), StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_sandboxed_step_asks_its_host_for_the_step_status_line()
    {
        _ = await Make(_sandbox).Launch(Step(), CancellationToken.None);

        Expect.Equal(SandboxStatusLine.Template(attended: false), _host.Started[0].StatusRight);
    }

    [Fact]
    public async Task An_unsandboxed_session_asks_for_no_status_line()
    {
        _ = await Make(sandbox: null).Launch(Step(), CancellationToken.None);

        Expect.True(_host.Started[0].StatusRight is null);
    }

    [Fact]
    public async Task A_refused_sandbox_fails_the_launch_with_its_code_and_starts_nothing()
    {
        _sandbox.Refuse = SindriErrors.LaunchUnconfinable.With("no");

        var result = await Make(_sandbox).Launch(Step(), CancellationToken.None);

        Expect.True(result is FailedLaunch { Error.Code: var code } && string.Equals(code, "SND-LAUNCH-015", StringComparison.Ordinal));
        Expect.Equal(0, _host.Started.Count);
        Expect.Equal(1, _sandbox.Released.Count);   // what a half-prepared sandbox left is released
    }

    [Fact]
    public async Task Stop_releases_the_sandbox_and_a_failing_release_does_not_fail_the_stop()
    {
        var request = Step();
        var launcher = Make(_sandbox);
        _ = await launcher.Launch(request, CancellationToken.None);
        _sandbox.ReleaseThrows = true;

        var stopped = await launcher.Stop(request.Launch, CancellationToken.None);

        Expect.True(stopped.IsSuccess);
        Expect.True(_sandbox.Released.Contains(request.Launch));
    }
}
