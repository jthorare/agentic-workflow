using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Toolkits;
using Aw.Sindri.Infrastructure.Toolkits;
using Aw.Sindri.Testing.Sandbox;
using Aw.Sindri.Tests.Sandbox.Launch;

namespace Aw.Sindri.Tests.Toolkits;

/// <summary>The launcher cleans up after itself (agent-first PR 16): a plan whose wrap failed leaves no run area behind.</summary>
[Lane(TestLane.Integration)]
public sealed class ToolkitProcessTests : IDisposable
{
    private readonly string _dir = Path.Combine(AppContext.BaseDirectory, "toolkit-fixtures", RealTime.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public async Task A_wrap_that_fails_after_the_plan_succeeded_discards_the_plan()
    {
        _ = Directory.CreateDirectory(Path.Combine(_dir, "bin"));
        var ct = TestContext.Current.CancellationToken;
        await File.WriteAllTextAsync(Path.Combine(_dir, "Notes.Toolkit.csproj"), "<Project />", ct);
        await File.WriteAllTextAsync(Path.Combine(_dir, "bin", "Notes.Toolkit"), string.Empty, ct);
        var sandbox = new RecordingSandbox(new WrapFails());
        var launcher = new ToolkitProcess(sandbox, new SandboxLaunchStagesTests.StubInputs("/policy"));

        var launched = await launcher.Launch(new Toolkit("notes", _dir, ToolkitState.Built, "h", DateTimeOffset.UnixEpoch, Manifest: null, "human"), ct);

        Assert.Equal(SindriErrors.ToolkitSandboxUnavailable.Code, launched.Error.Code);
        _ = Assert.Single(sandbox.Discarded);
    }

    private sealed class WrapFails : ISandbox
    {
        private readonly PassThroughSandbox _inner = new();

        public Result<SandboxCapabilities> Probe() => _inner.Probe();

        public Result<SandboxPlan> Plan(SandboxRequest request) => _inner.Plan(request);

        public Result<SandboxPlan> PlanProcess(ProcessSandboxRequest request) => _inner.PlanProcess(request);

        public Result<IReadOnlyList<string>> WrapProcess(SandboxPlan plan, IReadOnlyList<string> argv) => SindriErrors.ToolkitSandboxUnavailable.With("wrap failed");

        public Task<Result<Unit>> SelfCheck(SandboxPlan plan, CancellationToken ct) => _inner.SelfCheck(plan, ct);

        public Result<IReadOnlyList<string>> Wrap(SandboxPlan plan, IReadOnlyList<string> argv) => _inner.Wrap(plan, argv);

        public void Discard(SandboxPlan plan) => _inner.Discard(plan);
    }
}
