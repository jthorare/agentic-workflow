using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Toolkits;
using Aw.Sindri.Features.BuildToolkit;
using Aw.Sindri.Features.StartToolkit;

namespace Aw.Sindri.Tests.BuildToolkit;

/// <summary>BuildToolkit (agent-first PR 16): the source hash is recorded only when `dotnet build -warnaserror` and the toolkit's tests both pass, and a start needs that hash to match the source on disk.</summary>
[Lane(TestLane.Unit)]
public sealed class BuildToolkitTests : IDisposable
{
    private readonly ToolkitRig _rig = new();

    private static CancellationToken Token => ToolkitRig.Token;

    public void Dispose() => _rig.Dispose();

    private BuildToolkitService Build() => new(_rig.Store, _rig.Workspace, _rig.Commands, _rig.Slots, _rig.Jobs, TimeProvider.System);

    private StartToolkitService Start() => new(_rig.Store, _rig.Workspace, _rig.Runtime);

    [Theory]
    [InlineData("build")]
    [InlineData("test")]
    public async Task A_toolkit_that_fails_warnaserror_or_its_tests_is_not_startable(string failing)
    {
        _rig.Commands.Exits[failing] = 1;

        var built = await Build().Handle(new BuildToolkitRequest("triage"), Token);
        var started = await Start().Handle(new StartToolkitRequest("triage"), Token);

        Assert.True(built.IsError);
        Assert.Equal(SindriErrors.ToolkitTestsFailed.Code, built.Error.Code);
        Assert.True(started.IsError);
        Assert.Equal(SindriErrors.ToolkitNotBuilt.Code, started.Error.Code);
        Assert.Equal(ToolkitState.Failed, _rig.Store.Items["triage"].State);
        Assert.Null(_rig.Store.Items["triage"].SourceHash);
        Assert.Empty(_rig.Runtime.Started);
        Assert.Equal(0, _rig.Slots.Held);
    }

    [Fact]
    public async Task A_good_build_records_the_source_hash_under_a_heavy_slot_and_the_toolkit_starts()
    {
        var built = await Build().Handle(new BuildToolkitRequest("triage"), Token);
        var started = await Start().Handle(new StartToolkitRequest("triage"), Token);

        Assert.True(built.IsSuccess);
        Assert.Equal("hash-1", built.Value.SourceHash);
        // #2711: the folder holds the toolkit project and its tests in a sub-folder, with no solution, so both commands name the tests project.
        Assert.Equal(
            ["dotnet build Triage.Toolkit.Tests/Triage.Toolkit.Tests.csproj -warnaserror", "dotnet test --project Triage.Toolkit.Tests/Triage.Toolkit.Tests.csproj", "/tk/triage/bin/triage --manifest"],
            _rig.Commands.Ran);
        Assert.Equal(1, _rig.Slots.Taken);
        Assert.Equal(0, _rig.Slots.Held);
        Assert.True(started.IsSuccess);
        Assert.Equal(ToolkitState.Running, started.Value.State);
        Assert.Equal(["triage"], _rig.Runtime.Started);
    }

    [Fact]
    public async Task The_manifest_is_what_the_built_toolkit_prints_for_manifest_and_nothing_is_written_to_its_folder()
    {
        const string printed = """{"name":"triage","version":"2.0.0","resources":[],"tools":[]}""";
        _rig.Commands.Outputs["--manifest"] = printed;

        var built = await Build().Handle(new BuildToolkitRequest("triage"), Token);

        Assert.True(built.IsSuccess);
        Assert.Contains(_rig.Commands.Ran, static r => r.EndsWith(" --manifest", StringComparison.Ordinal));
        Assert.Equal(ToolkitState.Built, _rig.Store.Items["triage"].State);
        Assert.Equal("2.0.0", _rig.Store.Items["triage"].Manifest?.Version);
    }

    [Fact]
    public async Task A_warning_on_standard_error_during_manifest_does_not_break_the_manifest()
    {
        _rig.Commands.Errors["--manifest"] = "warn: using the default port";

        var built = await Build().Handle(new BuildToolkitRequest("triage"), Token);

        Assert.True(built.IsSuccess);
        Assert.Equal(ToolkitState.Built, _rig.Store.Items["triage"].State);
    }

    [Fact]
    public async Task A_failed_build_reports_what_the_compiler_wrote_on_standard_error()
    {
        _rig.Commands.Exits["build"] = 1;
        _rig.Commands.Errors["build"] = "CS1234: the error text";

        var built = await Build().Handle(new BuildToolkitRequest("triage"), Token);

        Assert.Contains("CS1234: the error text", built.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_toolkit_whose_manifest_command_fails_ends_failed_with_SND_TK_003()
    {
        _rig.Commands.Exits["--manifest"] = 1;

        var built = await Build().Handle(new BuildToolkitRequest("triage"), Token);

        Assert.True(built.IsError);
        Assert.Equal(SindriErrors.ToolkitManifestInvalid.Code, built.Error.Code);
        Assert.Contains(_rig.Commands.Ran, static r => r.EndsWith(" --manifest", StringComparison.Ordinal));
        Assert.Equal(ToolkitState.Failed, _rig.Store.Items["triage"].State);
        Assert.Null(_rig.Store.Items["triage"].SourceHash);
    }

    [Fact]
    public async Task A_toolkit_that_gave_up_after_crashes_starts_again_while_its_source_is_unchanged()
    {
        _ = await Build().Handle(new BuildToolkitRequest("triage"), Token);
        _rig.Store.Items["triage"] = _rig.Store.Items["triage"] with { State = ToolkitState.Failed };   // what the supervisor saves when it gives up: hash and manifest stay

        var started = await Start().Handle(new StartToolkitRequest("triage"), Token);

        Assert.True(started.IsSuccess);
        Assert.Equal(["triage"], _rig.Runtime.Started);
    }

    [Fact]
    public async Task A_start_after_the_source_changed_is_refused()
    {
        _ = await Build().Handle(new BuildToolkitRequest("triage"), Token);
        _rig.Workspace.Hash = "hash-2";

        var started = await Start().Handle(new StartToolkitRequest("triage"), Token);

        Assert.True(started.IsError);
        Assert.Equal(SindriErrors.ToolkitNotBuilt.Code, started.Error.Code);
    }

    [Fact]
    public async Task An_unknown_toolkit_is_SND_TK_009()
    {
        var built = await Build().Handle(new BuildToolkitRequest("nope"), Token);

        Assert.True(built.IsError);
        Assert.Equal(SindriErrors.ToolkitNotFound.Code, built.Error.Code);
    }
}
