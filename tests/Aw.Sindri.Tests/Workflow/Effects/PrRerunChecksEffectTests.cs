using Aw.Kernel.Testing;
using Aw.Sindri.Features.Blocks.Effect;

namespace Aw.Sindri.Tests.Workflow.Effects;

/// <summary>'effect.pr.rerun-checks' (PB spec §4.6): asks the forge to run the failed checks of the commit the snapshot saw them fail on; it replies nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class PrRerunChecksEffectTests
{
    private readonly PrEffectRig _rig = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_failed_checks_of_the_commit_they_ran_on_run_again()
    {
        var context = _rig.Context("effect.pr.rerun-checks", new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["pr"] = PrEffectRig.Fact($"\"{_rig.Link}\""),
            ["at"] = PrEffectRig.Fact(PrEffectRig.PrState("head-now", "ran-on")),
        });

        var done = await new PrRerunChecksEffect(_rig.Forge, _rig.Facts).Dispatch(context, Token);

        Assert.Null(done.Value.Reply);
        Assert.Equal((_rig.Link, "ran-on"), Assert.Single(_rig.Forge.Reruns));
    }

    [Fact]
    public async Task A_pull_request_the_forge_does_not_know_is_the_dispatch_error()
    {
        var context = _rig.Context("effect.pr.rerun-checks", new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["pr"] = PrEffectRig.Fact("\"https://example.test/o/r/pull/99\""),
            ["at"] = PrEffectRig.Fact(PrEffectRig.PrState("h", "h")),
        });

        var done = await new PrRerunChecksEffect(_rig.Forge, _rig.Facts).Dispatch(context, Token);

        Assert.True(done.IsError);
    }
}
