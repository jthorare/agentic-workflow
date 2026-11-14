using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Context.Concepts;
using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>The Context page's head (#3677, decision 2): the title says Context once, and the question box's label, placeholder and name all say "Ask a question".</summary>
[Lane(TestLane.Unit)]
public sealed class ContextHeaderTests
{
    [Fact]
    public async Task The_question_box_label_placeholder_and_accessibility_name_all_say_Ask_a_question()
    {
        var page = await ConceptPage.Made(new FakeSindriApi());

        Assert.Equal(("Ask a question", "Ask a question", "Ask a question"), (page.StartsAtLabel, page.AnchorHint, page.AnchorLabel));
    }

    [Fact]
    public async Task The_head_does_not_say_Context_again_next_to_the_title()
    {
        var page = await ConceptPage.Shown(ConceptPage.Serving(AConcepts.Three()));

        Assert.Equal(("Context", "Context · concepts", "concepts"), (page.Title, page.Head, page.HeadDetail));
    }

    [Fact]
    public void Labels_keep_the_question_words_in_one_place() =>
        Assert.Equal(("Ask a question", "Ask a question", "Ask a question"), (Labels.ContextMapStartsAt, Labels.ContextAnchorHint, Labels.ContextAnchor));
}
