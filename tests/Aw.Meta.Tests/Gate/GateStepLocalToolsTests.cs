using Aw.Meta.Core;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Gate;
using Aw.Meta.Features.GateRerun;
using Aw.Meta.Infrastructure.Processes;
using Aw.Meta.Tests.Adapters;

namespace Aw.Meta.Tests.Gate;

// #3441: the install step puts package tools in <tree>/node_modules/.bin; a step names them by their bare name.
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class GateStepLocalToolsTests : IDisposable
{
    private const string Marker = "local-tool-ran";
    private readonly TempDirectory _dir = new();

    public GateStepLocalToolsTests()
    {
        var bin = Directory.CreateDirectory(Path.Combine(_dir.Path, "node_modules", ".bin")).FullName;
        var tool = Path.Combine(bin, "aw-local-tool");
        File.WriteAllText(tool, "#!/bin/sh\necho " + Marker + "\n");
        File.SetUnixFileMode(tool, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
    }

    private MetaPaths Paths() => new(Path.Combine(_dir.Path, "state"), Path.Combine(_dir.Path, "gates"), Heavy: null, Gh: "gh");

    private string Log(string name) => Path.Combine(_dir.Path, name + ".log");

    [Fact, Trait("Lane", "integration")]
    public async Task A_step_finds_a_tool_the_install_put_in_the_trees_node_modules_bin()
    {
        var log = Log("step-a");

        var result = await new GateStepRunner(new SystemCommandRunner(), Paths()).Run(new StepConfig("fmt-check", ["bash", "-c", "aw-local-tool"]), _dir.Path, log, 1, CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.Equal(GateVerdict.Pass, result.Value.Verdict);
        Assert.Contains(Marker, await File.ReadAllTextAsync(log, TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_step_whose_first_word_is_the_tool_finds_it_too()
    {
        var log = Log("step-b");

        var result = await new GateStepRunner(new SystemCommandRunner(), Paths()).Run(new StepConfig("fmt-check", ["aw-local-tool"]), _dir.Path, log, 1, CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.Equal(GateVerdict.Pass, result.Value.Verdict);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_rerun_finds_the_trees_tools()
    {
        var job = new RerunJob(["bash", "-c", "aw-local-tool"], _dir.Path, "Some_test", FilterMustMatch: true, Log("rerun"));

        var result = await new RerunExecutor(new SystemCommandRunner(), Paths()).Run(job, CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.True(result.Value.Passed);
    }

    public void Dispose() => _dir.Dispose();
}
