using System.Text;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflow.Effects;

/// <summary>
/// 'effect.pr.merge' and 'effect.pr.reply' (PB spec §10.1, W23): the merge carries the snapshot's head as the expected head and maps the
/// forge's answer to 'pr.mergeResult' outcomes; a reply posts the drafted text once, a reaction reacts, and the forms that post nothing post nothing.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class PrMergeReplyEffectTests
{
    private const string Hidden = "swordfish-test-value";
    private const string Link = "https://forge.example/acme/web/pull/7";

    private readonly RecordingPulls _pulls = new();
    private readonly InMemoryFactStore _facts = new();
    private readonly InMemoryProfiles _profiles = new();
    private readonly InMemoryPrCommentMarks _marks = new();
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly PrRef _pr = new("forge.example", "acme/web", 7);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Merge_carries_the_snapshots_head_and_replies_merged()
    {
        var context = await Context("effect.pr.merge", new Dictionary<string, string>(StringComparer.Ordinal) { ["pr"] = "\"" + Link + "\"", ["at"] = """{"headSha":"deadbeef"}""" });

        var done = await new PrMergeEffect(_pulls, _facts, _profiles).Dispatch(context, Token);

        Assert.Equal(new EffectDone(OutcomeName.From("merged"), Existed: false), done.Value);
        Assert.Equal(new PullMerge(Link, PullMergeMethod.Squash, "deadbeef"), Assert.Single(_pulls.Merges));
    }

    [Fact]
    public async Task Merge_uses_the_method_the_recipe_names()
    {
        var context = await Context("effect.pr.merge", new Dictionary<string, string>(StringComparer.Ordinal) { ["pr"] = Link, ["at"] = """{"headSha":"deadbeef"}""" });
        context = context with { Arguments = new Dictionary<string, JsonElement>(context.Arguments, StringComparer.Ordinal) { ["method"] = Literal("rebase") } };

        _ = await new PrMergeEffect(_pulls, _facts, _profiles).Dispatch(context, Token);

        Assert.Equal(PullMergeMethod.Rebase, Assert.Single(_pulls.Merges).Method);
    }

    [Theory]
    [InlineData(PullMergeStatus.Blocked, "blocked")]
    [InlineData(PullMergeStatus.HeadMoved, "headMoved")]
    public async Task Merge_maps_a_refusal_to_its_outcome_and_never_forces(PullMergeStatus status, string outcome)
    {
        _pulls.MergeAnswer = new PullMergeOutcome(status, MergedSha: null, "why");
        var context = await Context("effect.pr.merge", new Dictionary<string, string>(StringComparer.Ordinal) { ["pr"] = Link, ["at"] = """{"headSha":"deadbeef"}""" });

        var done = await new PrMergeEffect(_pulls, _facts, _profiles).Dispatch(context, Token);

        Assert.Equal(OutcomeName.From(outcome), done.Value.Reply);
        _ = Assert.Single(_pulls.Merges);
    }

    [Fact]
    public async Task Merge_answers_the_forges_error()
    {
        _pulls.MergeAnswer = SindriErrors.ForgeUnavailable.With("down");
        var context = await Context("effect.pr.merge", new Dictionary<string, string>(StringComparer.Ordinal) { ["pr"] = Link, ["at"] = """{"headSha":"deadbeef"}""" });

        var done = await new PrMergeEffect(_pulls, _facts, _profiles).Dispatch(context, Token);

        Assert.True(done.IsError);
    }

    [Fact]
    public async Task Merge_without_a_head_in_the_snapshot_is_an_error_and_merges_nothing()
    {
        var context = await Context("effect.pr.merge", new Dictionary<string, string>(StringComparer.Ordinal) { ["pr"] = Link, ["at"] = """{"state":"open"}""" });

        var done = await new PrMergeEffect(_pulls, _facts, _profiles).Dispatch(context, Token);

        Assert.True(done.IsError);
        Assert.Empty(_pulls.Merges);
    }

    [Fact]
    public async Task A_thread_reply_posts_the_drafted_text_with_a_marker_derived_from_the_identity()
    {
        var context = await Context("effect.pr.reply", new Dictionary<string, string>(StringComparer.Ordinal) { ["form"] = "thread", ["comment"] = """{"id":"c1","key":"t9"}""", ["body"] = "\"Done, thanks.\"" });

        var done = await Reply().Dispatch(context, Token);

        Assert.Equal(new EffectDone(Reply: null, Existed: false), done.Value);
        Assert.Equal(new PullReply(Link, "t9", "Done, thanks.", "sindri:T-4-pr-1:3:0"), Assert.Single(_pulls.Replies));
    }

    [Fact]
    public async Task A_reply_the_forge_already_has_reports_existed()
    {
        _pulls.Delivery = PullDelivery.AlreadyDelivered;
        var context = await Context("effect.pr.reply", new Dictionary<string, string>(StringComparer.Ordinal) { ["form"] = "thread", ["comment"] = """{"id":"c1","key":"t9"}""", ["body"] = "\"Done.\"" });

        var done = await Reply().Dispatch(context, Token);

        Assert.True(done.Value.Existed);
    }

    [Fact]
    public async Task A_reaction_reacts_on_the_comments_thread()
    {
        var context = await Context("effect.pr.reply", new Dictionary<string, string>(StringComparer.Ordinal) { ["form"] = "reaction", ["comment"] = """{"id":"c1","key":"t9"}""" });

        _ = await Reply().Dispatch(context, Token);

        Assert.Equal(new PullReaction(Link, "t9", "+1", "sindri:T-4-pr-1:3:0"), Assert.Single(_pulls.Reactions));
    }

    [Theory]
    [InlineData("fixed")]
    [InlineData("hold")]
    [InlineData("none")]
    public async Task The_forms_that_post_nothing_post_nothing(string form)
    {
        var context = await Context("effect.pr.reply", new Dictionary<string, string>(StringComparer.Ordinal) { ["form"] = form, ["comment"] = """{"id":"c1","key":"t9"}""" });

        var done = await Reply().Dispatch(context, Token);

        Assert.True(done.IsSuccess);
        Assert.Empty(_pulls.Replies);
        Assert.Empty(_pulls.Reactions);
    }

    [Fact]
    public async Task A_thread_reply_writes_a_thread_mark_keyed_by_the_effect_identity()
    {
        var context = await Context("effect.pr.reply", new Dictionary<string, string>(StringComparer.Ordinal) { ["form"] = "thread", ["comment"] = """{"id":"c1","key":"t9","editedAt":"2026-10-21T08:00:00Z"}""", ["body"] = "\"Done.\"" });

        _ = await Reply().Dispatch(context, Token);
        var mark = Assert.Single((await _marks.Latest(_pr, Token)).Value);

        Assert.Equal(new PrCommentMark(_pr, PrCommentId.From("t9"), "2026-10-21T08:00:00Z", InstanceAddress.From("T-4/pr/1"), "thread", Reason: null, Commit: null, KnownInstants.Epoch, "T-4/pr/1:3:0"), mark);
    }

    [Fact]
    public async Task A_reaction_writes_a_reaction_mark()
    {
        var context = await Context("effect.pr.reply", new Dictionary<string, string>(StringComparer.Ordinal) { ["form"] = "reaction", ["comment"] = """{"id":"c1","key":"t9"}""" });

        _ = await Reply().Dispatch(context, Token);

        Assert.Equal("reaction", Assert.Single((await _marks.Latest(_pr, Token)).Value).Outcome);
    }

    [Fact]
    public async Task Hold_writes_a_held_mark_with_its_reason_and_posts_nothing()
    {
        var context = await Context("effect.pr.reply", new Dictionary<string, string>(StringComparer.Ordinal) { ["form"] = "hold", ["reason"] = "\"type\"", ["comment"] = """{"id":"c1","key":"t9"}""" });

        _ = await Reply().Dispatch(context, Token);
        var mark = Assert.Single((await _marks.Latest(_pr, Token)).Value);

        Assert.Equal(("held", "type"), (mark.Outcome, mark.Reason));
        Assert.Empty(_pulls.Replies);
    }

    [Fact]
    public async Task None_writes_a_none_mark()
    {
        var context = await Context("effect.pr.reply", new Dictionary<string, string>(StringComparer.Ordinal) { ["form"] = "none", ["comment"] = """{"id":"c1","key":"t9"}""" });

        _ = await Reply().Dispatch(context, Token);

        Assert.Equal("none", Assert.Single((await _marks.Latest(_pr, Token)).Value).Outcome);
    }

    [Fact]
    public async Task Fixed_writes_a_fixed_mark_with_the_fixs_commit()
    {
        var context = await Context("effect.pr.reply", new Dictionary<string, string>(StringComparer.Ordinal) { ["form"] = "fixed", ["comment"] = """{"id":"c1","key":"t9"}""", ["fix"] = """{"thread":"t9","commit":"abc1234"}""" });

        _ = await Reply().Dispatch(context, Token);
        var mark = Assert.Single((await _marks.Latest(_pr, Token)).Value);

        Assert.Equal(("fixed", "abc1234", "t9"), (mark.Outcome, mark.Commit, mark.CommentId.Value));
    }

    [Fact]
    public async Task A_redelivered_effect_writes_its_mark_once()
    {
        var context = await Context("effect.pr.reply", new Dictionary<string, string>(StringComparer.Ordinal) { ["form"] = "none", ["comment"] = """{"id":"c1","key":"t9"}""" });

        _ = await Reply().Dispatch(context, Token);
        _ = await Reply().Dispatch(context, Token);

        _ = Assert.Single((await _marks.Latest(_pr, Token)).Value);
    }

    [Fact]
    public async Task A_form_that_needs_a_comment_without_one_is_an_error_and_writes_nothing()
    {
        var context = await Context("effect.pr.reply", new Dictionary<string, string>(StringComparer.Ordinal) { ["form"] = "none" });

        var done = await Reply().Dispatch(context, Token);

        Assert.True(done.IsError);
        Assert.Empty((await _marks.Latest(_pr, Token)).Value);
    }

    [Fact]
    public async Task FixedReplies_answers_each_fixed_mark_the_push_carries_and_marks_it_replied()
    {
        _ = await _marks.Write(Marked("t9", "fixed", "abc1234", "e:1"), Token);
        _ = await _marks.Write(Marked("t8", "fixed", "ffff999", "e:2"), Token);
        var context = await Context("effect.pr.reply", new Dictionary<string, string>(StringComparer.Ordinal) { ["form"] = "fixedReplies", ["push"] = """{"sha":"abc1234","commits":["abc1234"]}""" });

        var done = await Reply().Dispatch(context, Token);
        var latest = (await _marks.Latest(_pr, Token)).Value;

        Assert.True(done.IsSuccess);
        Assert.Equal(new PullReply(Link, "t9", "Fixed in abc1234.", "sindri:T-4-pr-1:3:0:t9"), Assert.Single(_pulls.Replies));
        Assert.Equal("thread", latest.Single(m => m.CommentId.Value == "t9").Outcome);
        Assert.Equal("fixed", latest.Single(m => m.CommentId.Value == "t8").Outcome);
    }

    [Fact]
    public async Task FixedReplies_with_nothing_fixed_posts_nothing()
    {
        var context = await Context("effect.pr.reply", new Dictionary<string, string>(StringComparer.Ordinal) { ["form"] = "fixedReplies", ["push"] = """{"commits":["abc1234"]}""" });

        var done = await Reply().Dispatch(context, Token);

        Assert.True(done.IsSuccess);
        Assert.Empty(_pulls.Replies);
    }

    [Fact]
    public async Task A_thread_reply_is_scrubbed_before_it_reaches_the_forge()
    {
        var context = await Context("effect.pr.reply", new Dictionary<string, string>(StringComparer.Ordinal) { ["form"] = "thread", ["comment"] = """{"id":"c1","key":"t9"}""", ["body"] = "\"Value " + Hidden + " fixed.\"" });

        _ = await Reply().Dispatch(context, Token);

        Assert.Equal("Value [masked] fixed.", Assert.Single(_pulls.Replies).Text);
    }

    private sealed class Mask : ITextScrubber
    {
        public string Scrub(string text) => text.Replace(Hidden, "[masked]", StringComparison.Ordinal);
    }

    private PrReplyEffect Reply() => new(_pulls, _facts, _marks, _clock, new Mask());

    private PrCommentMark Marked(string comment, string outcome, string commit, string effect) =>
        new(_pr, PrCommentId.From(comment), EditedAt: null, InstanceAddress.From("T-4/pr/1"), outcome, Reason: null, commit, KnownInstants.Epoch, effect);

    private static JsonElement Literal(string text) => JsonDocument.Parse(JsonSerializer.Serialize(text)).RootElement.Clone();

    /// <summary>A context whose arguments are stored facts (a value that isn't JSON is stored as the text it is), with 'fact:pr' on the instance.</summary>
    private async Task<EffectContext> Context(string effect, Dictionary<string, string> arguments)
    {
        var resolved = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var (name, value) in arguments)
        {
            resolved[name] = name == "form" ? Literal(value) : await Stored(value);
        }

        var facts = new Dictionary<FactKey, JsonElement> { [FactKey.From("fact:pr")] = await Stored(Link) };
        return new EffectContext(BlockId.From(effect), "T-4/pr/1:3:0", "cmd-1", FromEngine: false, "T-4/pr/1", "T-4", Attempt: 1, resolved, facts, Text: null);
    }

    private async Task<JsonElement> Stored(string content)
    {
        var put = await _facts.Put(Encoding.UTF8.GetBytes(content), Token);
        return JsonDocument.Parse($$"""{"ref":"{{put.Value.Ref}}","sha256":"{{put.Value.Sha256}}"}""").RootElement.Clone();
    }
}
