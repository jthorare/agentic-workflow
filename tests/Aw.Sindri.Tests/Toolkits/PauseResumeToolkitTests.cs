using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Toolkits;
using Aw.Sindri.Features.PauseToolkit;
using Aw.Sindri.Features.ResumeToolkit;
using Aw.Sindri.Tests.BuildToolkit;

namespace Aw.Sindri.Tests.Toolkits;

/// <summary>PauseToolkit and ResumeToolkit (agent-first PR 16c): the runtime holds the pause, the row follows it, and a toolkit with no process can neither pause nor resume.</summary>
[Lane(TestLane.Unit)]
public sealed class PauseResumeToolkitTests : IDisposable
{
    private readonly ToolkitRig _rig = new();

    public PauseResumeToolkitTests()
    {
        _rig.Store.Items["triage"] = _rig.Store.Items["triage"] with { State = ToolkitState.Running, SourceHash = "hash-1", Manifest = ToolkitManifest.Parse(ToolkitRig.Manifest).Value };
    }

    private static CancellationToken Token => ToolkitRig.Token;

    public void Dispose() => _rig.Dispose();

    [Fact]
    public async Task A_pause_goes_to_the_runtime_and_the_row_says_paused_and_a_resume_puts_it_back()
    {
        var paused = await new PauseToolkitService(_rig.Store, _rig.Runtime).Handle(new PauseToolkitRequest("triage"), Token);
        var rowWhilePaused = _rig.Store.Items["triage"].State;
        var resumed = await new ResumeToolkitService(_rig.Store, _rig.Runtime).Handle(new ResumeToolkitRequest("triage"), Token);

        Assert.Equal(ToolkitState.Paused, paused.Value.State);
        Assert.Equal(ToolkitState.Paused, rowWhilePaused);
        Assert.Equal(["triage"], _rig.Runtime.Paused);
        Assert.Equal(ToolkitState.Running, resumed.Value.State);
        Assert.Equal(ToolkitState.Running, _rig.Store.Items["triage"].State);
        Assert.Equal(["triage"], _rig.Runtime.Resumed);
        Assert.Empty(_rig.Runtime.Stopped);   // a pause keeps the process
    }

    [Fact]
    public async Task A_toolkit_with_no_process_can_not_be_paused_or_resumed_and_its_row_stays()
    {
        _rig.Runtime.NotRunning.Add("triage");
        _rig.Store.Items["triage"] = _rig.Store.Items["triage"] with { State = ToolkitState.Built };

        var paused = await new PauseToolkitService(_rig.Store, _rig.Runtime).Handle(new PauseToolkitRequest("triage"), Token);
        var resumed = await new ResumeToolkitService(_rig.Store, _rig.Runtime).Handle(new ResumeToolkitRequest("triage"), Token);

        Assert.Equal(SindriErrors.ToolkitNotBuilt.Code, paused.Error.Code);
        Assert.Equal(SindriErrors.ToolkitNotBuilt.Code, resumed.Error.Code);
        Assert.Equal(ToolkitState.Built, _rig.Store.Items["triage"].State);
    }

    [Fact]
    public async Task An_unknown_toolkit_is_not_found_for_both()
    {
        var paused = await new PauseToolkitService(_rig.Store, _rig.Runtime).Handle(new PauseToolkitRequest("nope"), Token);
        var resumed = await new ResumeToolkitService(_rig.Store, _rig.Runtime).Handle(new ResumeToolkitRequest("nope"), Token);

        Assert.Equal(SindriErrors.ToolkitNotFound.Code, paused.Error.Code);
        Assert.Equal(SindriErrors.ToolkitNotFound.Code, resumed.Error.Code);
        Assert.Empty(_rig.Runtime.Paused);
    }
}
