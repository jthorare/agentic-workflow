using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>How a Context refusal is worded when "Did you mean" choices follow it or not (#4419).</summary>
[Lane(TestLane.Unit)]
public sealed class ContextRefusalWordsTests
{
    [Fact]
    public void A_miss_with_exactly_one_choice_says_no_item_is_named_it()
    {
        var line = Labels.ContextRefusal("SND-CONTEXT-003", "no node is named 'Invoice'", "Invoice", hasChoices: true);

        Assert.Equal("No item is named 'Invoice'.", line);
    }

    [Fact]
    public void A_several_match_refusal_is_worded_the_same_with_or_without_choices()
    {
        Assert.Equal("'Pane' names several items. Pick one.", Labels.ContextRefusal("SND-CONTEXT-037", "m", "Pane", hasChoices: false));
        Assert.Equal("'Pane' names several items. Pick one.", Labels.ContextRefusal("SND-CONTEXT-037", "m", "Pane", hasChoices: true));
    }
}
