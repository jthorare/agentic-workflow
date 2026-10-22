using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Features.Pr;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Blocks.Contracts;

namespace Aw.Sindri.Tests.Workflow.Pr;

/// <summary>The comment queues of the PR babysitter (spec §5.2, §5.3): authorship first, one thread a round, handled and held marks, and what an empty queue means.</summary>
[Lane(TestLane.Unit)]
public sealed class PrCommentQueueStepTests
{
    private static string Thread(string key, string author = "alice", string association = "MEMBER", bool isBot = false, string extra = "") =>
        $$"""{"key":"{{key}}","kind":"thread","author":"{{author}}","association":"{{association}}","isBot":{{(isBot ? "true" : "false")}},"resolved":false,"outdated":false,"path":"a.cs","body":"fix {{key}}","markers":[]{{extra}}}""";

    private static StepContext Context(string step, string comments, string state = "", string? bots = null)
    {
        var facts = new Dictionary<string, string>(StringComparer.Ordinal) { ["prState"] = $$"""{"comments":[{{comments}}]{{state}}}""" };
        var arguments = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (bots is not null)
        {
            arguments["bots"] = JsonDocument.Parse(bots).RootElement.Clone();
        }

        return BuildShipContexts.Of(step, facts) with { Arguments = arguments };
    }

    private static Task<BlockStepResult> Next(string comments, string state = "", string? bots = null, InMemoryPrCommentMarks? marks = null) =>
        new PrNextCommentStep(marks ?? new InMemoryPrCommentMarks()).Run(Context("step.pr.next-comment", comments, state, bots), TestContext.Current.CancellationToken).AsTask();

    private static Task<BlockStepResult> Held(string comments, string state = "", InMemoryPrCommentMarks? marks = null) =>
        new PrHeldCommentStep(marks ?? new InMemoryPrCommentMarks()).Run(Context("step.pr.held-comment", comments, state), TestContext.Current.CancellationToken).AsTask();

    private const string Url = ",\"url\":\"https://forge.example/acme/web/pull/7\"";

    private static readonly PrRef _pr = new("forge.example", "acme/web", 7);

    private static async Task<InMemoryPrCommentMarks> Marked(string comment, string outcome, string? editedAt = null)
    {
        var marks = new InMemoryPrCommentMarks();
        var mark = new PrCommentMark(_pr, PrCommentId.From(comment), editedAt, InstanceAddress.From("T-3/item/1"), outcome, Reason: null, Commit: null, new DateTimeOffset(2026, 10, 22, 9, 0, 0, TimeSpan.Zero), "e1");
        _ = await marks.Write(mark, TestContext.Current.CancellationToken);
        return marks;
    }

    [Fact]
    public async Task A_thread_marked_since_the_snapshot_is_not_taken_again()
    {
        var marks = await Marked("thread:1", "fixed");

        var result = await Next(Thread("thread:1") + "," + Thread("thread:2"), Url, marks: marks);

        Assert.Contains("thread:2", result.Result!.Outputs.Single().Value.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_queue_whose_threads_are_all_marked_is_drained()
    {
        var marks = await Marked("thread:1", "held");

        var result = await Next(Thread("thread:1"), Url, marks: marks);

        Assert.Equal("drained", result.Result?.Outcome.Value);
    }

    [Fact]
    public async Task A_mark_of_an_earlier_edit_does_not_hide_an_edited_thread()
    {
        var marks = await Marked("thread:1", "fixed", editedAt: "2026-10-20T10:00:00Z");

        var result = await Next(Thread("thread:1", extra: ",\"editedAt\":\"2026-10-21T10:00:00Z\""), Url, marks: marks);

        Assert.Equal("comment", result.Result?.Outcome.Value);
    }

    [Fact]
    public async Task A_thread_held_since_the_snapshot_is_in_the_held_walk()
    {
        var marks = await Marked("thread:3", "held");

        var result = await Held(Thread("thread:3"), Url, marks);

        Assert.Equal("comment", result.Result?.Outcome.Value);
    }

    [Fact]
    public async Task A_held_thread_answered_since_the_snapshot_leaves_the_held_walk()
    {
        var marks = await Marked("thread:3", "thread");

        var result = await Held(Thread("thread:3", extra: ",\"mark\":{\"outcome\":\"held\"}"), Url, marks);

        Assert.Equal("drained", result.Result?.Outcome.Value);
    }

    [Fact]
    public async Task The_oldest_unhandled_thread_is_taken_as_fact_comment()
    {
        var result = await Next(Thread("thread:1") + "," + Thread("thread:2"));

        Assert.Equal("comment", result.Result?.Outcome.Value);
        Assert.Contains("thread:1", result.Result!.Outputs.Single().Value.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_stranger_is_untrusted_before_anything_else()
    {
        var result = await Next(Thread("comment:7", author: "mallory", association: "NONE"));

        Assert.Equal("untrusted", result.Result?.Outcome.Value);
    }

    [Fact]
    public async Task A_bot_in_the_allowlist_is_a_bot_and_one_outside_it_is_untrusted()
    {
        var listed = await Next(Thread("thread:1", author: "scanner[bot]", association: "NONE", isBot: true), bots: """["scanner[bot]"]""");
        var unlisted = await Next(Thread("thread:1", author: "scanner[bot]", association: "NONE", isBot: true), bots: """["other[bot]"]""");

        Assert.Equal("bot", listed.Result?.Outcome.Value);
        Assert.Equal("untrusted", unlisted.Result?.Outcome.Value);
    }

    [Fact]
    public async Task Resolved_handled_held_and_own_threads_are_not_queued()
    {
        var resolved = Thread("thread:1").Replace("\"resolved\":false", "\"resolved\":true", StringComparison.Ordinal);
        var handled = Thread("thread:2", extra: ",\"mark\":{\"outcome\":\"fixed\"}");
        var held = Thread("thread:3", extra: ",\"mark\":{\"outcome\":\"held\"}");
        var own = Thread("thread:4", author: "sindri-bot");
        var viaMarker = Thread("thread:5").Replace("\"markers\":[]", "\"markers\":[\"sindri:reply:9\"]", StringComparison.Ordinal);

        var result = await Next(string.Join(",", resolved, handled, held, own, viaMarker, Thread("thread:6")), ",\"self\":\"sindri-bot\"");

        Assert.Contains("thread:6", result.Result!.Outputs.Single().Value.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_edited_comment_is_unhandled_again()
    {
        var edited = Thread("thread:1", extra: ",\"editedAt\":\"2026-10-21T10:00:00Z\",\"mark\":{\"outcome\":\"fixed\",\"editedAt\":\"2026-10-20T10:00:00Z\"}");

        var result = await Next(edited);

        Assert.Equal("comment", result.Result?.Outcome.Value);
    }

    [Fact]
    public async Task A_thread_without_editedAt_and_a_null_self_still_matches_its_mark()
    {
        var handled = Thread("thread:1", extra: ",\"mark\":{\"outcome\":\"fixed\",\"editedAt\":\"2026-10-20T10:00:00Z\"}");

        var result = await Next(handled, ",\"self\":null");

        Assert.Equal("drained", result.Result?.Outcome.Value);
    }

    [Theory]
    [InlineData("", "drained")]
    [InlineData(",\"workspaceAhead\":true", "drainedUnpushed")]
    public async Task An_empty_queue_is_drained_and_unpushed_when_the_workspace_is_ahead(string state, string outcome)
    {
        var result = await Next(string.Empty, state);

        Assert.Equal(outcome, result.Result?.Outcome.Value);
        Assert.Empty(result.Result!.Outputs);
    }

    [Fact]
    public async Task A_body_over_8_KiB_is_capped()
    {
        var long8 = Thread("thread:1").Replace("fix thread:1", new string('x', 9000), StringComparison.Ordinal);

        var result = await Next(long8);

        Assert.Equal(8192, result.Result!.Outputs.Single().Value.GetProperty("body").GetString()!.Length);
    }

    [Fact]
    public async Task No_prState_object_fails_the_Step()
    {
        var result = await new PrNextCommentStep(new Aw.Sindri.Testing.Workflow.InMemoryPrCommentMarks()).Run(BuildShipContexts.Of("step.pr.next-comment", []), TestContext.Current.CancellationToken);

        Assert.NotNull(result.Failure);
    }

    [Fact]
    public async Task The_held_walk_takes_the_oldest_held_comment_whatever_its_author()
    {
        var held = Thread("thread:2", author: "mallory", association: "NONE", extra: ",\"mark\":{\"outcome\":\"held\",\"reason\":\"untrusted\"}");

        var result = await Held(Thread("thread:1") + "," + held);

        Assert.Equal("comment", result.Result?.Outcome.Value);
        Assert.Contains("thread:2", result.Result!.Outputs.Single().Value.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_comment_re_held_after_the_walk_started_is_left_for_the_next_walk()
    {
        var before = Thread("thread:1", extra: ",\"mark\":{\"outcome\":\"held\",\"at\":\"2026-10-21T09:00:00Z\"}");
        var after = Thread("thread:2", extra: ",\"mark\":{\"outcome\":\"held\",\"at\":\"2026-10-21T11:00:00Z\"}");
        var state = ",\"walkStartedAt\":\"2026-10-21T10:00:00Z\"";

        var first = await Held(before + "," + after, state);
        var drained = await Held(after, state + ",\"workspaceAhead\":true");

        Assert.Contains("thread:1", first.Result!.Outputs.Single().Value.GetRawText(), StringComparison.Ordinal);
        Assert.Equal("drainedUnpushed", drained.Result?.Outcome.Value);
    }

    [Fact]
    public async Task With_nothing_held_the_walk_is_drained()
    {
        var result = await Held(Thread("thread:1"));

        Assert.Equal("drained", result.Result?.Outcome.Value);
    }
}
