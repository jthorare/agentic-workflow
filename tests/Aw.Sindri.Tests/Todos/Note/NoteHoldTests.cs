using Aw.Kernel.Testing;
using Aw.Sindri.Core.Todos.Note;
using static Aw.Sindri.Tests.Todos.Note.NoteSamples;

namespace Aw.Sindri.Tests.Todos.Note;

/// <summary>The re-read a hold's pick does first (todo spec §4.5): only changes still true in the note act, so Apply can't remove a line the owner just pasted back.</summary>
[Lane(TestLane.Unit)]
public sealed class NoteHoldTests
{
    private static NoteHold Hold(params HeldChange[] changes) => new(changes);

    [Fact]
    public void A_removal_is_still_true_while_no_line_carries_the_id()
    {
        var hold = Hold(Gone(1));

        Assert.Equal(hold.Changes, hold.StillTrue(Doc(L(2, "b"))));
    }

    [Fact]
    public void A_removal_is_not_true_once_the_line_is_pasted_back()
    {
        var hold = Hold(Gone(1), Gone(2));

        var still = hold.StillTrue(Doc(L(1, "a")));

        Assert.Equal([Gone(2)], still);
    }

    [Fact]
    public void An_edit_is_true_while_the_line_is_the_held_line_and_not_once_it_changed_again()
    {
        var hold = Hold(new HeldChange(Id(1), HeldKind.Edited, L(1, "edited")), new HeldChange(Id(2), HeldKind.Edited, L(2, "edited")));

        var still = hold.StillTrue(Doc(L(1, "edited"), L(2, "edited again")));

        Assert.Equal([new HeldChange(Id(1), HeldKind.Edited, L(1, "edited"))], still);
    }

    [Fact]
    public void A_reopen_is_not_true_once_the_line_is_ticked_again()
    {
        var hold = Hold(new HeldChange(Id(1), HeldKind.Reopened, L(1, "d")), new HeldChange(Id(2), HeldKind.Reopened, L(2, "d")));

        var still = hold.StillTrue(Doc(X(1, "d"), L(2, "d")));

        Assert.Equal([new HeldChange(Id(2), HeldKind.Reopened, L(2, "d"))], still);
    }

    [Fact]
    public void An_edit_whose_line_is_gone_is_not_true()
    {
        var hold = Hold(new HeldChange(Id(1), HeldKind.Edited, L(1, "edited")));

        Assert.Empty(hold.StillTrue(Doc("prose")));
    }

    [Fact]
    public void The_first_line_with_an_id_is_the_one_that_counts()
    {
        var hold = Hold(new HeldChange(Id(1), HeldKind.Edited, L(1, "edited")));

        _ = Assert.Single(hold.StillTrue(Doc(L(1, "edited"), L(1, "a copy"))));
    }
}
