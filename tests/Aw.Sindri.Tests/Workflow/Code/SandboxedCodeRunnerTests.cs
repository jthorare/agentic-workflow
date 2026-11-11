using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Infrastructure.Workflow.Code;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Sandbox;

namespace Aw.Sindri.Tests.Workflow.Code;

/// <summary>What the sandboxed code runner hands the sandbox: the dll's path per mechanism, and a runner binary that is not there.</summary>
[Lane(TestLane.Unit)]
public sealed class SandboxedCodeRunnerTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("aw-sandboxed-code-runner-");

    public void Dispose() => _directory.Delete(recursive: true);

    private static CodeRun Run() => new("return 1;", "null", Repo: null, Branch: null, TimeSpan.FromSeconds(30), MemoryMb: 256);

    private (SandboxedCodeRunner Runner, ArgvRecordingSandbox Sandbox, string RunAreas) Build(SandboxMechanism mechanism, string runnerPath)
    {
        var sandbox = new ArgvRecordingSandbox(mechanism);
        var runAreas = Path.Combine(_directory.FullName, "areas");
        var cache = new CompiledCodeCache(new CodeCompiler(), Path.Combine(_directory.FullName, "cache"));
        return (new SandboxedCodeRunner(cache, new InMemoryProfiles(), sandbox, static () => SandboxRequests.Host, runAreas, runnerPath), sandbox, runAreas);
    }

    private string ExistingRunner()
    {
        var path = Path.Combine(_directory.FullName, "sindri-code-runner");
        File.WriteAllText(path, string.Empty);
        return path;
    }

    [Fact]
    public async Task On_Seatbelt_the_dll_is_passed_by_its_real_path_under_the_run_area()
    {
        var (runner, sandbox, runAreas) = Build(SandboxMechanism.Seatbelt, ExistingRunner());

        _ = await runner.Run(Run(), TestContext.Current.CancellationToken);

        var dll = Assert.Single(sandbox.Argvs)[1];
        Assert.True(Path.IsPathRooted(dll));
        Assert.StartsWith(runAreas, dll, StringComparison.Ordinal);
        Assert.EndsWith(Path.Combine("tmp", "code.dll"), dll, StringComparison.Ordinal);
    }

    [Fact]
    public async Task On_bubblewrap_the_dll_is_passed_as_the_mounted_tmp_path()
    {
        var (runner, sandbox, _) = Build(SandboxMechanism.Bubblewrap, ExistingRunner());

        _ = await runner.Run(Run(), TestContext.Current.CancellationToken);

        Assert.Equal("/tmp/code.dll", Assert.Single(sandbox.Argvs)[1]);
    }

    [Fact]
    public async Task A_runner_binary_that_is_missing_is_SND_CODE_005_and_nothing_is_planned()
    {
        var missing = Path.Combine(_directory.FullName, "no-such-runner");
        var (runner, sandbox, _) = Build(SandboxMechanism.Bubblewrap, missing);

        var result = await runner.Run(Run(), TestContext.Current.CancellationToken);

        Assert.Equal("SND-CODE-005", result.Error.Code);
        Assert.Contains(missing, result.Error.Message, StringComparison.Ordinal);
        Assert.Equal(0, sandbox.Plans);
    }

    /// <summary>Plans like the real planner for a chosen mechanism, records each argv it is asked to wrap, and refuses, so no process starts.</summary>
    private sealed class ArgvRecordingSandbox(SandboxMechanism mechanism) : ISandbox
    {
        public List<IReadOnlyList<string>> Argvs { get; } = [];

        public int Plans { get; private set; }

        public Result<SandboxCapabilities> Probe() => new SandboxCapabilities(mechanism, "test");

        public Result<SandboxPlan> Plan(SandboxRequest request) => SandboxPlanner.Build(mechanism, request);

        public Result<SandboxPlan> PlanProcess(ProcessSandboxRequest request)
        {
            Plans++;
            return ProcessSandboxPlanner.Build(mechanism, request);
        }

        public Result<IReadOnlyList<string>> WrapProcess(SandboxPlan plan, IReadOnlyList<string> argv)
        {
            Argvs.Add(argv);
            return SindriErrors.CodeRunnerUnavailable.With("recorded, not started");
        }

        public Task<Result<Unit>> SelfCheck(SandboxPlan plan, CancellationToken ct) => Task.FromResult<Result<Unit>>(Unit.Value);

        public Result<IReadOnlyList<string>> Wrap(SandboxPlan plan, IReadOnlyList<string> argv) => Results.Success(argv);

        public void Discard(SandboxPlan plan)
        {
        }
    }
}
