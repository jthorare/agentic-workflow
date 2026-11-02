using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Features.GetRunSuspension;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.SessionReports;

namespace Aw.Sindri.Tests.GetRunSuspension;

/// <summary>'GET /launches/{launch}/suspended' (sandbox spec §7, SB16): proven by the launch secret; an unavailable answer is an error the caller reads as not suspended.</summary>
[Lane(TestLane.Unit)]
public sealed class GetRunSuspensionTests
{
    private readonly SessionHarness _s = new();
    private readonly FakeRunSuspensions _suspensions = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private GetRunSuspensionService Service() => new(_s.Events(), _suspensions);

    [Fact]
    public async Task A_wrong_launch_secret_is_refused()
    {
        await _s.Started();
        _suspensions.Suspend(A.FirstLaunchId);

        var result = await Service().Handle(A.GetRunSuspensionRequest().WithProof("not-the-proof").Build(), Token);

        Expect.True(result.IsError && string.Equals(result.Error.Code, SindriErrors.AuthLaunchSecret.Code, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_answer_is_the_engines(bool suspended)
    {
        await _s.Started();
        if (suspended)
        {
            _suspensions.Suspend(A.FirstLaunchId);
        }

        var result = await Service().Handle(A.GetRunSuspensionRequest().Build(), Token);

        Expect.True(result.IsSuccess && result.Value.Suspended == suspended);
    }

    [Fact]
    public async Task An_answer_the_engine_cannot_give_is_an_error()
    {
        await _s.Started();
        _suspensions.Unavailable = true;

        var result = await Service().Handle(A.GetRunSuspensionRequest().Build(), Token);

        Expect.True(result.IsError);
    }
}
