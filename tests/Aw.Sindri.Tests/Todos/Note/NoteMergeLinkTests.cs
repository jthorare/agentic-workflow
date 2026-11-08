using Aw.Kernel.Testing;
using Aw.Sindri.Core.Todos.Note;
using static Aw.Sindri.Tests.Todos.Note.NoteSamples;

namespace Aw.Sindri.Tests.Todos.Note;

/// <summary>Links the note can't show (a workflow address) stay on the todo when the owner edits the links the note can.</summary>
[Lane(TestLane.Unit)]
public sealed class NoteMergeLinkTests
{
    private const string Pr = "sindri://reviews/o/r/12";
    private const string Workflow = "sindri://workflow/a-b";

    private static string WithPrChip(int n) => $"- [ ] a [PR #12]({Pr}) {Block(n)}";

    [Fact]
    public void Deleting_a_pr_chip_keeps_the_links_the_note_cannot_show()
    {
        var todo = S(1, "a") with { Links = [Pr, Workflow] };

        var r = NoteMerge.Merge(Base(WithPrChip(1)), Doc(L(1, "a")), [todo], Options());

        Same([$"change 1 links=[{Workflow}]"], Intents(r));
    }

    [Fact]
    public void Adding_a_link_keeps_the_hidden_ones_after_it()
    {
        var todo = S(1, "a") with { Links = [Workflow] };
        var note = $"- [ ] a [ENG-12](sindri://linear/ENG-12) {Block(1)}";

        var r = NoteMerge.Merge(Base(L(1, "a")), Doc(note), [todo], Options());

        Same([$"change 1 links=[sindri://linear/ENG-12,{Workflow}]"], Intents(r));
    }

    [Fact]
    public void Deleting_the_only_chip_of_a_todo_with_no_hidden_links_clears_them()
    {
        var todo = S(1, "a") with { Links = [Pr] };

        var r = NoteMerge.Merge(Base(WithPrChip(1)), Doc(L(1, "a")), [todo], Options());

        Same(["change 1 links=[]"], Intents(r));
    }
}
