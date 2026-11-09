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
        Assert.Equal(["dotnet build -warnaserror", "dotnet test"], _rig.Commands.Ran);
        Assert.Equal(1, _rig.Slots.Taken);
        Assert.Equal(0, _rig.Slots.Held);
        Assert.True(started.IsSuccess);
        Assert.Equal(ToolkitState.Running, started.Value.State);
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
