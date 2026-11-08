using Aw.Kernel.Testing;
using Aw.Sindri.Core.Todos.Note;
using static Aw.Sindri.Tests.Todos.Note.NoteSamples;

namespace Aw.Sindri.Tests.Todos.Note;

/// <summary>Which note lines the merge adopts, leaves as text, or writes next to, and where it appends (todo spec §4.5).</summary>
[Lane(TestLane.Unit)]
public sealed class NoteMergeLineTests
{
    private static NoteMergeResult Run(string[] noteBase, string[] note, SindriTodo[] sindri, NoteMergeOptions? options = null) =>
        NoteMerge.Merge(Base(noteBase), Doc(note), sindri, options ?? Options());

    [Fact]
    public void A_bare_line_that_ends_in_a_block_id_of_its_own_is_left_as_text()
    {
        var r = Run([], ["- [ ] a ^abc123"], []);

        Assert.Empty(r.Intents);
        Assert.Empty(r.LineEdits);
        Assert.Empty(r.NextBase);
        Assert.Equal(0, r.Adopted);
    }

    [Fact]
    public void A_known_id_line_that_gained_a_block_id_of_its_own_is_blocked()
    {
        var r = Run([L(1, "a")], [$"- [ ] a ^abc123 {Block(1)}"], [S(1, "a")]);

        Same(["blocked 1 line 1: it ends in a block id (^…) of its own"], Intents(r));
        Assert.Empty(r.LineEdits);
    }

    [Fact]
    public void Text_with_a_trailing_space_in_sindri_is_not_a_change_on_every_pass()
    {
        var r = Run([L(1, "a")], [L(1, "a")], [S(1, "a ")]);

        Assert.Empty(r.Intents);
        Assert.Empty(r.LineEdits);
        Same([L(1, "a")], r.NextBase);
    }
}
