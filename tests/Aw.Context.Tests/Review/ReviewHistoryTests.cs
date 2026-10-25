using Aw.Context.Core.Overrides;
using Aw.Context.Core.Review;

namespace Aw.Context.Tests.Review;

public sealed class ReviewHistoryTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_history_lists_every_override_newest_first_and_marks_the_ones_an_undo_cancelled()
    {
        var accept = SampleDrafts.Row(OverrideAction.Accept, "feature:layout", at: SampleDrafts.At);
        var reject = SampleDrafts.Row(OverrideAction.Reject, "feature:tabs.groups", at: SampleDrafts.At.AddMinutes(1));
        var undo = SampleDrafts.Undo(reject, SampleDrafts.At.AddMinutes(2));

        var history = ReviewHistory.Of([accept, undo, reject]);

        Assert.Equal([undo.ToId(), reject.ToId(), accept.ToId()], history.Entries.Select(e => e.Id));
        Assert.Equal([false, true, false], history.Entries.Select(e => e.Undone));
        Assert.Equal(reject.ToId(), history.Entries[0].Undoes);
        Assert.Equal(("feature:layout", "avery"), (history.Entries[2].Target.Value, history.Entries[2].Who));
    }

    [Fact, Trait("Lane", "unit")]
    public void No_overrides_is_an_empty_history() => Assert.Empty(ReviewHistory.Of([]).Entries);

    [Fact, Trait("Lane", "unit")]
    public void A_limit_keeps_the_newest_entries_and_says_when_older_ones_were_cut()
    {
        var rows = Enumerable.Range(0, 5).Select(i => SampleDrafts.Row(OverrideAction.Accept, "feature:n" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), at: SampleDrafts.At.AddMinutes(i))).ToArray();

        var cut = ReviewHistory.Of(rows, limit: 2);
        var all = ReviewHistory.Of(rows, limit: 5);

        Assert.Equal([rows[4].ToId(), rows[3].ToId()], cut.Entries.Select(e => e.Id));
        Assert.True(cut.Truncated);
        Assert.Equal((5, false), (all.Entries.Count, all.Truncated));
        Assert.Equal(int.MaxValue, ReviewHistory.DefaultLimit);   // no limit named: everything
    }
}
