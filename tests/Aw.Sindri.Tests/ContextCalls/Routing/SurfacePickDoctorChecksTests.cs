using Aw.Context.Core.Engine;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Host.Models;

namespace Aw.Sindri.Tests.ContextCalls.Routing;

/// <summary>The doctor's 'context:surfacepick' row (spec 8.4): ok off, ok on with a key, warn on without one.</summary>
public sealed class SurfacePickDoctorChecksTests
{
    private static async Task<DoctorCheck> Row(LabelPick pick, string? key, LabelPick labelPick = LabelPick.Off)
    {
        var checks = new SurfacePickDoctorChecks(new ChoiceRig.SwitchableModels(labelPick) { SurfacePick = pick }, new JevKey(name => name == "TYPESAFE_API_KEY" ? key : null, () => throw new InvalidOperationException("no keychain on this path"), isMacOS: false));
        return Assert.Single(await checks.Run(TestContext.Current.CancellationToken));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Off_is_ok_whatever_the_key_and_whatever_the_label_pick_says()
    {
        var row = await Row(LabelPick.Off, key: null, labelPick: LabelPick.Jev);

        Assert.Equal(("context:surfacepick", CheckState.Ok, "product surface choice is off"), (row.Name, row.State, row.Detail));
        Assert.Null(row.Fix);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task On_with_a_key_is_ok()
    {
        var row = await Row(LabelPick.Jev, key: "tsk_test");

        Assert.Equal(("context:surfacepick", CheckState.Ok, "product surface choice is on"), (row.Name, row.State, row.Detail));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task On_without_a_key_warns_and_names_the_fix()
    {
        var row = await Row(LabelPick.Jev, key: null);

        Assert.Equal(CheckState.Warn, row.State);
        Assert.Equal("product surface choice is on, Jev unavailable: product surfaces stay as the build found them", row.Detail);
        Assert.Equal("set TYPESAFE_API_KEY where sindri runs", row.Fix);
    }
}
