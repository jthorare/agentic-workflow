using Aw.Sindri.Core;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.RestartProcess;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.RestartProcess;

/// <summary>POST /sindri/restart (first-run spec decision 11, A4): the owner's; it reads the open set first and refuses with SND-SETUP-009 naming each open run, stopping nothing.</summary>
public sealed class RestartProcessTests
{
    private readonly SliceHarness _slice = new();
    private readonly RecordingStop _stop = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task Restart_refuses_and_names_two_open_runs()
    {
        var open = new FixedOpenRuns(
            new OpenRun("workflow-run", "nightly-gate@1:14", "Nightly gate (run 14, an agent step)", "sindri://workflow/nightly-gate/runs/nightly-gate@1:14"),
            new OpenRun("job", "job-3", "Code index of agentic-workflow (a job)", "sindri://job/job-3"));
        using var entered = _slice.Callers.Enter(A.Caller().Human().Build());

        var result = await new RestartProcessService(_slice.Callers, open, _stop).Handle(new RestartProcessRequest(), Token);

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.SetupRestartRunsOpen.Code, result.Error.Code);
        Assert.Contains("Nightly gate (run 14, an agent step)", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("Code index of agentic-workflow (a job)", result.Error.Message, StringComparison.Ordinal);
        Assert.Equal(0, _stop.Calls);   // nothing open is ever stopped
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Restart_from_a_session_is_refused()
    {
        using var entered = _slice.Callers.Enter(A.Caller().Launched().Build());

        var result = await new RestartProcessService(_slice.Callers, new FixedOpenRuns(), _stop).Handle(new RestartProcessRequest(), Token);

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.AuthRefused.Code, result.Error.Code);
        Assert.Equal(0, _stop.Calls);
    }
}
