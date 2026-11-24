using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>The Control plane card says its own state (#4035): a problem elsewhere (a context source) leaves it Healthy while the pill says Degraded; a problem of the control plane itself keeps it Degraded.</summary>
[Lane(TestLane.Unit)]
public sealed class ControlPlaneCardWordTests
{
    [Fact]
    public void A_flagged_context_source_leaves_the_control_plane_card_healthy_while_the_pill_says_degraded()
    {
        var pill = Shown(new HealthProblemView("SND-CONTEXT-071", "linear: Linear refused the key (401), retrying", "the linear source needs a look"));

        Assert.Equal(Labels.HealthWord("degraded"), pill.Word, StringComparer.Ordinal);
        Assert.Equal(Labels.HealthWord("healthy"), pill.ProcessWord, StringComparer.Ordinal);
        Assert.Equal(StateGlyph.Ok, pill.ProcessGlyph);
        Assert.DoesNotContain("linear", pill.ProcessText, StringComparison.Ordinal);
    }

    [Fact]
    public void A_stale_build_of_the_control_plane_keeps_its_card_degraded()
    {
        var pill = Shown(new HealthProblemView("SND-BUILD-001", "the running build is not the installed one", "the running build is not the installed one"));

        Assert.Equal(Labels.HealthWord("degraded"), pill.ProcessWord, StringComparer.Ordinal);
        Assert.Equal(StateGlyph.Warning, pill.ProcessGlyph);
    }

    private static HealthPill Shown(HealthProblemView problem)
    {
        var pill = new HealthPill();
        pill.Show(ShellState.Normal, TimeSpan.FromSeconds(2), notResponding: false, new HealthView("degraded", [problem]));
        return pill;
    }
}
