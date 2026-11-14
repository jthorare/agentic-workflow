using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>#3695: every band's count words pluralize, the Concepts band's too.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextWholeKindCountTests
{
    [Fact]
    public void The_concepts_band_count_says_concept_once_and_concepts_otherwise()
    {
        Expect.Equal(("1 concept", "2 concepts", "0 concepts"), (Labels.ContextWholeKindCount("concept", 1), Labels.ContextWholeKindCount("concept", 2), Labels.ContextWholeKindCount("concept", 0)));
    }

    [Fact]
    public void The_other_bands_keep_their_plurals()
    {
        Expect.Equal(("4 features", "2 code files", "1 code file"), (Labels.ContextWholeKindCount("feature", 4), Labels.ContextWholeKindCount("code", 2), Labels.ContextWholeKindCount("code", 1)));
    }
}
