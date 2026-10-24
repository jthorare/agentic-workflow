using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Item;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Item;

/// <summary>A disabled decision option says why (review run 3, a11y-r3-0): the reason and the command, on the decision sheet (UI spec §7).</summary>
[Lane(TestLane.Unit)]
public sealed class DecideReadinessTests
{
    private readonly FakeTimeProvider _clock = new(A.Noon);

    [Fact]
    public void The_decision_sheet_options_carry_the_reason_and_the_command()
    {
        var served = NeedsYouRows.Decision();
        var pending = served with { Actions = [.. served.Actions.Select(a => a with { Readiness = ActionReadiness.Pending, Cli = "sindri decide DEC-1 " + a.OptionId })] };
        var sheet = new DecisionSheetViewModel(new FakeSindriApi(), () => null, _clock, new DecisionSender(new FakeSindriApi()));

        sheet.Show(new NeedsYouRowViewModel(pending, seen: true, reducedMotion: false, arrived: false, _clock));

        Assert.All(sheet.Options, o => Assert.False(o.IsEnabled));
        Assert.Equal(["sindri decide DEC-1 accept", "sindri decide DEC-1 decline"], sheet.Options.Select(o => o.Reason), StringComparer.Ordinal);
        Assert.Contains("sindri decide DEC-1 accept", sheet.DecideLine, StringComparison.Ordinal);
    }
}
