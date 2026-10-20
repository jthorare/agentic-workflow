using Aw.Sindri.Host.Commands.Line;
using Aw.Sindri.Testing.Line;
using Aw.Sindri.Tests.Design;

namespace Aw.Sindri.Tests.Commands;

/// <summary>A session caller reads the stop view's model- and person-written text inside the pack fence (LN-API2); the ids, enums and numbers stay as they are.</summary>
public sealed class DesignStopFencingTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task The_texts_are_inside_the_fence_and_the_structure_is_untouched()
    {
        var view = await DesignStopViewTests.Show(LineFixture.Plan());

        var fenced = DesignStopFencing.Fence(view);

        Assert.NotEqual(view.Spec.Text, fenced.Spec.Text, StringComparer.Ordinal);
        Assert.Contains(view.Spec.Text, fenced.Spec.Text, StringComparison.Ordinal);
        Assert.NotEqual(view.Decisions[0].Question, fenced.Decisions[0].Question, StringComparer.Ordinal);
        Assert.NotEqual(view.Decisions[0].Options[0].Answer, fenced.Decisions[0].Options[0].Answer, StringComparer.Ordinal);
        Assert.NotEqual(view.Acceptance.Added[0].Text, fenced.Acceptance.Added[0].Text, StringComparer.Ordinal);
        Assert.NotEqual(view.Trace[0].Signals[0].Statement, fenced.Trace[0].Signals[0].Statement, StringComparer.Ordinal);
        Assert.NotEqual(view.Contracts[0].Quote, fenced.Contracts[0].Quote, StringComparer.Ordinal);
        Assert.NotEqual(view.LensRounds[0].Lenses[1].Findings[0].Text, fenced.LensRounds[0].Lenses[1].Findings[0].Text, StringComparer.Ordinal);
        Assert.Equal(view.Decisions[0].Decision, fenced.Decisions[0].Decision, StringComparer.Ordinal);
        Assert.Equal(view.Graph.Waves, fenced.Graph.Waves);
        Assert.Equal(view.NotYet, fenced.NotYet, StringComparer.Ordinal);
        Assert.Equal(view.Graph.Phases[0].Phase, fenced.Graph.Phases[0].Phase, StringComparer.Ordinal);
    }
}
