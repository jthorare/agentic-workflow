using Aw.Kernel.Testing;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.ModelCheck;
using Aw.Sindri.Infrastructure.ModelCheck;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Workflow.ModelCheck;

/// <summary>Doctor's 'model:check' line: ok with what tlc.sh says, a warning with the fix when java or the jar is missing, a warning when the runner isn't installed. Never a failure.</summary>
[Lane(TestLane.Integration)]
public sealed class ModelCheckDoctorChecksTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aw-model-doctor-" + Path.GetRandomFileName());

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ModelCheckSetup Installed()
    {
        _ = Directory.CreateDirectory(_root);
        var tlc = Path.Combine(_root, "tlc.sh");
        File.WriteAllText(tlc, "#!/bin/sh\n");
        return new ModelCheckSetup("/runner/tlc-check.sh", "/usr/bin:/bin", "/home/u", "/home/u/.state", tlc, "2g", "/tmp/m");
    }

    [Fact]
    public async Task A_working_tlc_sh_doctor_is_ok_with_what_it_says()
    {
        var runner = new FakeToolRunner(new ToolRun(ToolOutcome.Exited, 0, "tlc: ok java 21, tla2tools 1.7.4\n"));

        var rows = await new ModelCheckDoctorChecks(Installed(), runner).Run(Token);

        var row = Assert.Single(rows);
        Assert.Equal(("model:check", CheckState.Ok, "ok java 21, tla2tools 1.7.4"), (row.Name, row.State, row.Detail));
        var call = Assert.Single(runner.Calls);
        Assert.Equal(("env", "-i"), (call.FileName, call.Arguments[0]));   // run with the PATH the step will see
        Assert.Contains("PATH=/usr/bin:/bin", call.Arguments, StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_missing_java_is_a_warning_with_the_fix_and_never_a_failure()
    {
        var runner = new FakeToolRunner(new ToolRun(ToolOutcome.Exited, 3, "tlc: java isn't installed (fix: brew install openjdk@21)\n"));

        var rows = await new ModelCheckDoctorChecks(Installed(), runner).Run(Token);

        var row = Assert.Single(rows);
        Assert.Equal(CheckState.Warn, row.State);
        Assert.Contains("java isn't installed", row.Detail, StringComparison.Ordinal);
        Assert.Contains("stops for you", row.Detail, StringComparison.Ordinal);
        Assert.Equal("brew install openjdk@21", row.Fix);
    }

    [Fact]
    public async Task A_runner_that_is_not_installed_is_a_warning_and_runs_nothing()
    {
        var runner = new FakeToolRunner(new ToolRun(ToolOutcome.Exited, 0, string.Empty));

        var rows = await new ModelCheckDoctorChecks(Installed() with { Runner = null }, runner).Run(Token);

        Assert.Equal(CheckState.Warn, Assert.Single(rows).State);
        Assert.Empty(runner.Calls);
    }
}
