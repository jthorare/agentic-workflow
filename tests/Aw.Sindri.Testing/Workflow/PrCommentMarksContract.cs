using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Xunit;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>
/// The contract every <see cref="IPrCommentMarks"/> honours (PR babysitter spec §5.4, W26): the latest row per
/// (comment, edited-at) is the comment's state, an effect identity writes once, and a purge keeps the rows of a PR another
/// instance still names.
/// </summary>
public abstract class PrCommentMarksContract
{
    private static readonly PrRef _pr = new("file", "o/r", 7);
    private static readonly DateTimeOffset _t0 = new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A fresh store with no marks.</summary>
    /// <returns>The store.</returns>
    protected abstract IPrCommentMarks Create();

    private static PrCommentMark Mark(string comment, string outcome, int minutes, string effect, string? edited = null, PrRef? pr = null) =>
        new(pr ?? _pr, PrCommentId.From(comment), edited, InstanceAddress.From("T-3/item/1"), outcome, Reason: outcome == "hold" ? "type" : null, Commit: null, _t0.AddMinutes(minutes), effect);

    /// <summary>A PR nothing was marked on has no marks.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_pr_without_marks_has_none()
    {
        var marks = Create();

        Expect.True((await marks.Latest(_pr, Token)).Value.Count == 0);
    }

    /// <summary>A written mark reads back whole.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_written_mark_reads_back()
    {
        var marks = Create();
        var mark = Mark("c1", "hold", 0, "T-3/item/1:e:0", edited: "2026-10-21T08:00:00+00:00");

        var written = await marks.Write(mark, Token);
        var latest = await marks.Latest(_pr, Token);

        Expect.True(written.Value);
        Expect.Equal(mark, Assert.Single(latest.Value));
    }

    /// <summary>The later row of a comment is its state.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_latest_row_per_comment_is_its_state()
    {
        var marks = Create();
        _ = await marks.Write(Mark("c1", "hold", 0, "e:0"), Token);
        _ = await marks.Write(Mark("c1", "thread", 5, "e:1"), Token);
        _ = await marks.Write(Mark("c2", "reaction", 1, "e:2"), Token);

        var latest = (await marks.Latest(_pr, Token)).Value;

        Expect.Equal(2, latest.Count);
        Expect.Equal("thread", latest.Single(m => m.CommentId.Value == "c1").Outcome);
        Expect.Equal("reaction", latest.Single(m => m.CommentId.Value == "c2").Outcome);
    }

    /// <summary>An edited comment is a different key, so it is unhandled again.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_edited_comment_is_a_new_key()
    {
        var marks = Create();
        _ = await marks.Write(Mark("c1", "thread", 0, "e:0"), Token);
        _ = await marks.Write(Mark("c1", "hold", 1, "e:1", edited: "2026-10-21T09:30:00+00:00"), Token);

        var latest = (await marks.Latest(_pr, Token)).Value;

        Expect.Equal(2, latest.Count);
        Expect.True(latest.Exists(m => m.EditedAt is null && m.Outcome == "thread"));
        Expect.True(latest.Exists(m => m.EditedAt is not null && m.Outcome == "hold"));
    }

    /// <summary>Writing the same effect identity twice records one row.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_redelivered_effect_writes_nothing_more()
    {
        var marks = Create();
        _ = await marks.Write(Mark("c1", "thread", 0, "e:0"), Token);

        var again = await marks.Write(Mark("c1", "none", 9, "e:0"), Token);

        Expect.True(again.IsSuccess && !again.Value);
        Expect.Equal("thread", Assert.Single((await marks.Latest(_pr, Token)).Value).Outcome);
    }

    /// <summary>Marks are per pull request.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Pull_requests_are_marked_independently()
    {
        var marks = Create();
        var other = new PrRef("file", "o/r", 8);
        _ = await marks.Write(Mark("c1", "thread", 0, "e:0"), Token);
        _ = await marks.Write(Mark("c1", "hold", 0, "e:1", pr: other), Token);

        Expect.Equal("thread", Assert.Single((await marks.Latest(_pr, Token)).Value).Outcome);
        Expect.Equal("hold", Assert.Single((await marks.Latest(other, Token)).Value).Outcome);
    }

    /// <summary>A purge deletes one PR's rows and no other's.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_purge_deletes_the_rows_of_that_pr_only()
    {
        var marks = Create();
        var other = new PrRef("file", "o/r", 8);
        _ = await marks.Write(Mark("c1", "thread", 0, "e:0"), Token);
        _ = await marks.Write(Mark("c1", "hold", 0, "e:1", pr: other), Token);

        var purged = await marks.Purge(_pr, anotherInstanceNamesPr: false, Token);

        Expect.Equal(1, purged.Value);
        Expect.True((await marks.Latest(_pr, Token)).Value.Count == 0);
        Expect.Equal(1, (await marks.Latest(other, Token)).Value.Count);
    }

    /// <summary>While another instance names the PR its rows stay.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_purge_keeps_the_rows_while_another_instance_names_the_pr()
    {
        var marks = Create();
        _ = await marks.Write(Mark("c1", "thread", 0, "e:0"), Token);

        var purged = await marks.Purge(_pr, anotherInstanceNamesPr: true, Token);

        Expect.Equal(0, purged.Value);
        Expect.Equal(1, (await marks.Latest(_pr, Token)).Value.Count);
    }
}
