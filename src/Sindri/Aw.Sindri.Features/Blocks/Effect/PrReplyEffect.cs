using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>
/// 'effect.pr.reply' (PB spec §5, §10.1): the 'thread' form posts 'body' on the comment's thread, the 'reaction' form reacts to it, both once per effect
/// identity (Sindri's reply marker is derived from it, and the forge says when it was already delivered). The 'fixed', 'hold' and 'none' forms post
/// nothing. Every form writes its mark on delivery (spec §5.4, W26) keyed by the effect identity, so a re-delivery writes none twice; 'fixedReplies' answers
/// each 'fixed' mark whose commit the push carried (replying on the mark's thread key) and marks it 'thread'. The recipe's transitions don't wait on a reply,
/// so it replies nothing.
/// </summary>
/// <param name="pulls">The pull requests port.</param>
/// <param name="facts">Reads 'fact:pr', 'comment' and 'body' back.</param>
/// <param name="marks">The comment marks (W26).</param>
/// <param name="clock">Stamps a mark.</param>
/// <param name="scrubber">Takes secrets out of a reply's text before the forge gets it.</param>
internal sealed class PrReplyEffect(IPullRequests pulls, IFactContents facts, IPrCommentMarks marks, TimeProvider clock, ITextScrubber scrubber) : IEffectKind, IDescribedBlock
{
    private const int MarkerLimit = 128;

    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => PrEffects.Reply;

    /// <inheritdoc/>
    public async ValueTask<Result<EffectDone>> Dispatch(EffectContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var form = context.Arguments.TryGetValue("form", out var named) ? PrReplyFacts.Text(named) : null;
        return form switch
        {
            "thread" => await Post(context, reaction: false, cancellationToken).ConfigureAwait(false),
            "reaction" => await Post(context, reaction: true, cancellationToken).ConfigureAwait(false),
            "fixed" or "hold" or "none" => await MarkOnly(context, form, cancellationToken).ConfigureAwait(false),
            "fixedReplies" => await ReplyFixed(context, cancellationToken).ConfigureAwait(false),
            _ => WorkflowErrors.NoRuntime.With($"effect.pr.reply of {context.Instance} has no form '{form}' (thread, reaction, fixed, fixedReplies, none or hold)"),
        };
    }

    private async ValueTask<Result<EffectDone>> Post(EffectContext context, bool reaction, CancellationToken cancellationToken)
    {
        var read = await ReadPost(context, reaction, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var (link, thread, text, target) = read.Value;
        var marker = Marker(context.Identity);
        var delivered = reaction
            ? await pulls.React(new PullReaction(link, thread, text, marker), cancellationToken).ConfigureAwait(false)
            : await pulls.Reply(new PullReply(link, thread, scrubber.Scrub(text), marker), cancellationToken).ConfigureAwait(false);
        if (delivered.IsError)
        {
            return delivered.Error;
        }

        var written = await Record(target, reaction ? "reaction" : "thread", new MarkDetail(Reason: null, Commit: null, Effect: context.Identity), cancellationToken).ConfigureAwait(false);
        return written.IsError ? written.Error : new EffectDone(Reply: null, Existed: delivered.Value == PullDelivery.AlreadyDelivered);
    }

    private async ValueTask<Result<PostInput>> ReadPost(EffectContext context, bool reaction, CancellationToken cancellationToken)
    {
        var pr = await PrReplyFacts.Fact(facts, context, "fact:pr", cancellationToken).ConfigureAwait(false);
        var comment = await PrReplyFacts.Argument(facts, context, "comment", cancellationToken).ConfigureAwait(false);
        var body = reaction ? PrReplyFacts.Undefined : await PrReplyFacts.Argument(facts, context, "body", cancellationToken).ConfigureAwait(false);
        if (PrReplyFacts.FirstError(pr, comment, body) is { } failed)
        {
            return failed;
        }

        var link = PrReplyFacts.Text(pr.Value, "link", "url", "pr");
        var thread = PrReplyFacts.Text(comment.Value, "key", "threadId", "thread");
        var text = reaction ? "+1" : PrReplyFacts.Text(body.Value, "body", "text", "reply");
        if (string.IsNullOrEmpty(link) || string.IsNullOrEmpty(thread) || string.IsNullOrWhiteSpace(text))
        {
            return WorkflowErrors.NoRuntime.With($"effect.pr.reply of {context.Instance} needs the pull request ('fact:pr'), the comment's thread key and, for a thread reply, its body");
        }

        var target = Target(context, link, thread, PrReplyFacts.Text(comment.Value, "editedAt", "edited_at"));
        return target.IsError ? target.Error : new PostInput(link, thread, text, target.Value);
    }

    /// <summary>The 'fixed', 'hold' and 'none' forms: post nothing, write the mark ('fixed' carries the fix's commit, 'hold' its reason).</summary>
    private async ValueTask<Result<EffectDone>> MarkOnly(EffectContext context, string form, CancellationToken cancellationToken)
    {
        var pr = await PrReplyFacts.Fact(facts, context, "fact:pr", cancellationToken).ConfigureAwait(false);
        var comment = await PrReplyFacts.Argument(facts, context, "comment", cancellationToken).ConfigureAwait(false);
        var fix = await PrReplyFacts.Argument(facts, context, "fix", cancellationToken).ConfigureAwait(false);
        var reason = await PrReplyFacts.Argument(facts, context, "reason", cancellationToken).ConfigureAwait(false);
        if (PrReplyFacts.FirstError(pr, comment, fix, reason) is { } failed)
        {
            return failed;
        }

        var id = PrReplyFacts.Text(fix.Value, "thread") ?? PrReplyFacts.Text(comment.Value, "key", "threadId", "thread");
        var target = Target(context, PrReplyFacts.Text(pr.Value, "link", "url", "pr"), id, PrReplyFacts.Text(comment.Value, "editedAt", "edited_at"));
        if (target.IsError)
        {
            return target.Error;
        }

        var detail = new MarkDetail(PrReplyFacts.Text(reason.Value, "reason"), form == "fixed" ? PrReplyFacts.Text(fix.Value, "commit", "sha") : null, context.Identity);
        var outcome = form == "hold" ? "held" : form;   // spec 5.3: the form is 'hold', the mark's outcome 'held'
        var written = await Record(target.Value, outcome, detail, cancellationToken).ConfigureAwait(false);
        return written.IsError ? written.Error : new EffectDone(Reply: null, Existed: !written.Value);
    }

    /// <summary>The 'fixedReplies' form: answers each comment whose latest mark is 'fixed' with a commit the push carried, then marks it 'thread'.</summary>
    private async ValueTask<Result<EffectDone>> ReplyFixed(EffectContext context, CancellationToken cancellationToken)
    {
        var read = await ReadFixed(context, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var (link, carried) = read.Value;
        var anchor = Target(context, link, "-", editedAt: null);
        if (anchor.IsError)
        {
            return anchor.Error;
        }

        var latest = await marks.Latest(anchor.Value.Pr, cancellationToken).ConfigureAwait(false);
        if (latest.IsError)
        {
            return latest.Error;
        }

        var replies = 0;
        var existed = true;
        foreach (var mark in latest.Value.Where(m => CarriesFix(m, carried)))
        {
            var replied = await ReplyOne(context, link, mark, cancellationToken).ConfigureAwait(false);
            if (replied.IsError)
            {
                return replied.Error;
            }

            replies++;
            existed &= replied.Value;
        }

        return new EffectDone(Reply: null, Existed: replies > 0 && existed);
    }

    private async ValueTask<Result<(string Link, List<string> Carried)>> ReadFixed(EffectContext context, CancellationToken cancellationToken)
    {
        var pr = await PrReplyFacts.Argument(facts, context, "pr", cancellationToken).ConfigureAwait(false);
        var push = await PrReplyFacts.Argument(facts, context, "push", cancellationToken).ConfigureAwait(false);
        var fallback = pr.IsSuccess && pr.Value.ValueKind != System.Text.Json.JsonValueKind.Undefined ? pr : await PrReplyFacts.Fact(facts, context, "fact:pr", cancellationToken).ConfigureAwait(false);
        if (PrReplyFacts.FirstError(fallback, push) is { } failed)
        {
            return failed;
        }

        var link = PrReplyFacts.Text(fallback.Value, "link", "url", "pr");
        return string.IsNullOrEmpty(link)
            ? WorkflowErrors.NoRuntime.With($"effect.pr.reply of {context.Instance} needs a pull request link ('.../pull/N') and the comment's thread key to mark it")
            : (link, PrReplyFacts.Commits(push.Value));
    }

    private static bool CarriesFix(PrCommentMark mark, List<string> carried) =>
        mark.Outcome == "fixed" && mark.Commit is { Length: > 0 } c && carried.Exists(x => x.StartsWith(c, StringComparison.Ordinal) || c.StartsWith(x, StringComparison.Ordinal));

    private async ValueTask<Result<bool>> ReplyOne(EffectContext context, string link, PrCommentMark mark, CancellationToken cancellationToken)
    {
        var commit = mark.Commit!;   // why: the caller filtered to marks with a commit
        var effect = $"{context.Identity}:{mark.CommentId.Value}";
        var text = $"Fixed in {(commit.Length > 7 ? commit[..7] : commit)}.";
        var delivered = await pulls.Reply(new PullReply(link, mark.CommentId.Value, scrubber.Scrub(text), Marker(effect)), cancellationToken).ConfigureAwait(false);
        if (delivered.IsError)
        {
            return delivered.Error;
        }

        var target = new MarkTarget(mark.Pr, mark.CommentId, mark.EditedAt, mark.Instance);
        var written = await Record(target, "thread", new MarkDetail(Reason: null, Commit: commit, Effect: effect), cancellationToken).ConfigureAwait(false);
        return written.IsError ? written.Error : delivered.Value == PullDelivery.AlreadyDelivered;
    }

    private ValueTask<Result<bool>> Record(MarkTarget target, string outcome, MarkDetail detail, CancellationToken cancellationToken) =>
        marks.Write(new PrCommentMark(target.Pr, target.Comment, target.EditedAt, target.Instance, outcome, detail.Reason, detail.Commit, clock.GetUtcNow(), detail.Effect), cancellationToken);

    /// <summary>The mark's key from the PR link and the comment: forge host, 'owner/name' and number from '.../owner/name/pull/N'.</summary>
    private static Result<MarkTarget> Target(EffectContext context, string? link, string? comment, string? editedAt)
    {
        var parts = Uri.TryCreate(link, UriKind.Absolute, out var uri) ? uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries) : [];
        if (parts.Length < 4 || parts[^2] != "pull" || !int.TryParse(parts[^1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var number)
            || !PrCommentId.TryFrom(comment, out var id) || !InstanceAddress.TryFrom(context.Instance, out var instance))
        {
            return WorkflowErrors.NoRuntime.With($"effect.pr.reply of {context.Instance} needs a pull request link ('.../pull/N') and the comment's thread key to mark it");
        }

        return new MarkTarget(new PrRef(uri!.Host, $"{parts[^4]}/{parts[^3]}", number), id, string.IsNullOrEmpty(editedAt) ? null : editedAt, instance);   // why: a parsed link has a uri
    }

    private readonly record struct PostInput(string Link, string Thread, string Text, MarkTarget Target);

    private readonly record struct MarkTarget(PrRef Pr, PrCommentId Comment, string? EditedAt, InstanceAddress Instance);

    private readonly record struct MarkDetail(string? Reason, string? Commit, string Effect);

    /// <summary>Sindri's reply marker for an effect identity: the forge allows letters, digits, '.', '_', ':' and '-' (128 at most), so anything else becomes '-'.</summary>
    private static string Marker(string identity)
    {
        var marker = new StringBuilder("sindri:");
        foreach (var c in identity)
        {
            _ = marker.Append(char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or ':' or '-' ? c : '-');
        }

        return marker.Length > MarkerLimit ? marker.ToString(0, MarkerLimit) : marker.ToString();
    }
}
