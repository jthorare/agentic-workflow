using Aw.Context.Core.Engine;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Host.Models;

namespace Aw.Sindri.Tests.ContextCalls.Routing;

/// <summary>The doctor's 'context:labelpick' row (spec 8.4): ok off, ok on with a key, warn on without one.</summary>
public sealed class LabelPickDoctorChecksTests
{
    private static async Task<DoctorCheck> Row(LabelPick pick, string? key)
    {
        var checks = new LabelPickDoctorChecks(new ChoiceRig.SwitchableModels(pick), name => name == "TYPESAFE_API_KEY" ? key : null);
        return Assert.Single(await checks.Run(TestContext.Current.CancellationToken));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Off_is_ok_whatever_the_key()
    {
        var row = await Row(LabelPick.Off, key: null);

        Assert.Equal(("context:labelpick", CheckState.Ok, "label choice is off"), (row.Name, row.State, row.Detail));
        Assert.Null(row.Fix);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task On_with_a_key_is_ok()
    {
        var row = await Row(LabelPick.Jev, key: "tsk_test");

        Assert.Equal(("context:labelpick", CheckState.Ok, "label choice is on"), (row.Name, row.State, row.Detail));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task On_without_a_key_warns_and_names_the_fix()
    {
        Assert.SkipWhen(OperatingSystem.IsMacOS(), "macOS keeps the key in the keychain, which the doctor does not read");

        var row = await Row(LabelPick.Jev, key: null);

        Assert.Equal(CheckState.Warn, row.State);
        Assert.Equal("label choice is on, Jev unavailable: labels take the model's first candidate", row.Detail);
        Assert.Equal("set TYPESAFE_API_KEY where sindri runs", row.Fix);
    }
}
