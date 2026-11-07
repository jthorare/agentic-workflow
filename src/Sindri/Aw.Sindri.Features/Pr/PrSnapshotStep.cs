using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Pr;

/// <summary>
/// 'step.pr.snapshot' on the block runtime (babysitter spec §4.3): one forge read of the pull request and its conversation, reported as the first outcome that holds, in the
/// spec's order, with 'fact:prState' and 'fact:prStatus' (the merge-stop summary) on every outcome and 'fact:mergedSha' when merged (the merge commit 'fact:merge' holds when this instance merged it; else the head sha, an approximation: the forge port has no merge commit on a view). The fact is the pull request view in camelCase (link, state, headSha, mergeable,
/// reviewDecision, unresolvedThreads, checks { state, sha, failing }), 'pullState' (open, closed or merged), 'comments' (the threads, oldest first, as the queue Steps parse
/// them), 'self' (null), 'queue' counts, 'mergeBlockedBy' and 'at'. 'state' is 'green' when the outcome is green (the merge gate reads it), else the pull request's own state.
/// Each comment carries its latest mark (outcome, reason, commit, at) from the comment marks, queue.held counts the comments whose latest mark is 'hold'; a marked comment is handled and is not in queue.unhandled or the 'comments' outcome, and 'stale' says the checks ran on another commit than the head.
/// The docs pull request ('fact:docsPr', when the instance holds one) is read too: a merged pull request whose docs pull request is open is 'mergedDocsOpen', a red docs pull request is 'docsRed'
/// (after the comments, before pending; not when the owner skipped the docs, 'fact:docsResult' result 'skippedByOwner'), and checks still running on it keep the snapshot pending; 'docs' carries it
/// (link, state, headSha, checks). 'workspaceAhead' says the workspace ('fact:workspace') is on another commit than the pull request's head (null when its head can't be read).
/// 'base' ({ ref, sha, tip, was }, see <see cref="PrBaseFact"/>) carries the base branch and the tip the pull request was built on, which effect.pr.restack replays after. Not read yet, for want of a port: the default branch, so 'restack' is never reported and 'base.defaultRef', 'base.onto' and 'head.ref' are not written.
/// </summary>
/// <param name="pulls">The forge, through Sindri's port.</param>
/// <param name="marks">The comment marks.</param>
/// <param name="diff">Reads the workspace's head.</param>
/// <param name="clock">The clock.</param>
/// <param name="record">The merge record, where a changed snapshot is appended.</param>
internal sealed class PrSnapshotStep(IPullRequests pulls, IPrCommentMarks marks, IRunDiff diff, TimeProvider clock, IMergeRecord record) : IBlockStep, IBlockStepId
{
    private static readonly string[] _trusted = ["OWNER", "MEMBER", "COLLABORATOR"];

    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.pr.snapshot");

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (PrLink.Of(context) is not { } link)
        {
            return BlockStepResult.Failed("step.pr.snapshot needs the pull request's link (fact:pr), which the instance does not hold");
        }

        var pull = await pulls.Get(link, cancellationToken).ConfigureAwait(false);
        if (pull.IsError)
        {
            return BlockStepResult.Failed($"step.pr.snapshot can't read {link}: {pull.Error.Message}");
        }

        var threads = await pulls.Threads(link, cancellationToken).ConfigureAwait(false);
        if (threads.IsError)
        {
            return BlockStepResult.Failed($"step.pr.snapshot can't read the conversation of {link}: {threads.Error.Message}");
        }

        var latest = await LatestMarks(link, cancellationToken).ConfigureAwait(false);
        if (latest.IsError)
        {
            return BlockStepResult.Failed($"step.pr.snapshot can't read the comment marks of {link}: {latest.Error.Message}");
        }

        var docs = await ReadDocs(context, cancellationToken).ConfigureAwait(false);
        if (docs.IsError)
        {
            return BlockStepResult.Failed($"step.pr.snapshot can't read the docs pull request of {link}: {docs.Error.Message}");
        }

        var work = threads.Value.Where(t => !t.Resolved && !Own(t) && !latest.Value.ContainsKey(t.Key)).ToList();   // a marked comment is handled (spec: a mark not superseded by an edit); the forge port has no login, so Sindri's replies are told apart by their markers alone
        var outcome = Outcome(pull.Value, work.Count, docs.Value);
        var state = State(pull.Value, outcome, new Conversation(threads.Value, work, latest.Value), docs.Value);
        state["base"] = PrBaseFact.Of(pull.Value, StepFacts.Read(context, "fact:prState"), StepFacts.Read(context, "fact:restack"));
        var status = PrStatusFact.Of(StepFacts.Read(context, "fact:recheck"), StepFacts.Read(context, "fact:docsResult"), pull.Value, threads.Value, docs.Value?.View);
        return await Report(context, new Snapshot(pull.Value, outcome, state), status, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<BlockStepResult> Report(StepContext context, Snapshot snapshot, JsonObject status, CancellationToken cancellationToken)
    {
        var (pull, outcome, state) = snapshot;
        state["workspaceAhead"] = await WorkspaceAhead(context, pull.HeadSha, cancellationToken).ConfigureAwait(false);
        state["outcome"] = outcome;
        var recorded = await Record(context, snapshot, cancellationToken).ConfigureAwait(false);
        if (recorded.IsError)
        {
            return BlockStepResult.Failed($"step.pr.snapshot can't append to the merge record: {recorded.Error.Message}");
        }

        return string.Equals(outcome, "merged", StringComparison.Ordinal) || string.Equals(outcome, "mergedDocsOpen", StringComparison.Ordinal)
            ? StepFacts.Done(outcome, ("fact:prState", state), ("fact:prStatus", status), ("fact:mergedSha", JsonValue.Create(StepFacts.Field(StepFacts.Read(context, "fact:merge"), "mergedSha") ?? pull.HeadSha)))
            : StepFacts.Done(outcome, ("fact:prState", state), ("fact:prStatus", status));
    }

    // The snapshot row (spec §4.7), when the outcome or the head differs from the previous snapshot's. Its identity is the previous snapshot's time, so a retry of this Step writes it once.
    private async ValueTask<Result<bool>> Record(StepContext context, Snapshot snapshot, CancellationToken cancellationToken)
    {
        var (pull, outcome, state) = snapshot;
        var previous = StepFacts.Read(context, "fact:prState");
        if (context.Address is not { Length: > 0 } address
            || (string.Equals(StepFacts.Field(previous, "outcome"), outcome, StringComparison.Ordinal) && string.Equals(StepFacts.Field(previous, "headSha"), pull.HeadSha, StringComparison.Ordinal)))
        {
            return false;
        }

        var fields = new JsonObject { ["outcome"] = outcome, ["checks"] = state["checks"]?.DeepClone(), ["queue"] = state["queue"]?.DeepClone() };
        var identity = $"{address}:snapshot:{StepFacts.Field(previous, "at") ?? "first"}";
        return await record.Append(new MergeRow(address, identity, "snapshot", pull.HeadSha, fields), cancellationToken).ConfigureAwait(false);
    }

    private sealed record Snapshot(PullView Pull, string Outcome, JsonObject State);

    // The docs pull request the instance holds, with whether the owner skipped the docs; null when it holds none.
    private async ValueTask<Result<DocsRead?>> ReadDocs(StepContext context, CancellationToken cancellationToken)
    {
        var fact = StepFacts.Read(context, "fact:docsPr");
        var link = fact switch
        {
            JsonValue value when value.TryGetValue<string>(out var text) => text,
            JsonObject obj => StepFacts.Field(obj, "link") ?? StepFacts.Field(obj, "url"),
            _ => null,
        };
        if (string.IsNullOrWhiteSpace(link))
        {
            return (DocsRead?)null;
        }

        var view = await pulls.Get(link.Trim(), cancellationToken).ConfigureAwait(false);
        var skipped = string.Equals(StepFacts.Field(StepFacts.Read(context, "fact:docsResult"), "result"), "skippedByOwner", StringComparison.Ordinal);
        return view.IsError ? view.Error : new DocsRead(view.Value, skipped);
    }

    // Whether the workspace is on another commit than the pull request's head; null when its head can't be read.
    private async ValueTask<JsonNode?> WorkspaceAhead(StepContext context, string headSha, CancellationToken cancellationToken)
    {
        if (StepFacts.Text(context, "fact:workspace") is not { Length: > 0 } workspace)
        {
            return JsonValue.Create(value: false);
        }

        var head = await diff.Head(workspace, cancellationToken).ConfigureAwait(false);
        return head.IsError ? null : JsonValue.Create(!string.Equals(head.Value.Trim(), headSha, StringComparison.Ordinal));
    }

    // The latest mark per comment across its edits, keyed by the thread key (a mark's CommentId).
    private async ValueTask<Result<Dictionary<string, PrCommentMark>>> LatestMarks(string link, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, PrCommentMark>(StringComparer.Ordinal);
        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri))
        {
            return result;
        }

        var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4 || parts[^2] != "pull" || !int.TryParse(parts[^1], NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            return result;
        }

        var rows = await marks.Latest(new PrRef(uri.Host, $"{parts[^4]}/{parts[^3]}", number), cancellationToken).ConfigureAwait(false);
        if (rows.IsError)
        {
            return rows.Error;
        }

        foreach (var row in rows.Value)
        {
            var key = row.CommentId.Value;
            if (!result.TryGetValue(key, out var seen) || row.At >= seen.At)
            {
                result[key] = row;
            }
        }

        return result;
    }

    private static string Outcome(PullView pull, int unhandled, DocsRead? docs)
    {
        if (pull.State != PullState.Open)
        {
            return pull.State == PullState.Merged ? (docs?.View.State == PullState.Open ? "mergedDocsOpen" : "merged") : "closed";
        }

        return pull.Mergeable == false ? "conflict" : OpenOutcome(pull, unhandled, docs);
    }

    private static string OpenOutcome(PullView pull, int unhandled, DocsRead? docs)
    {
        var onHead = string.Equals(pull.Checks.Sha, pull.HeadSha, StringComparison.Ordinal);
        if (onHead && pull.Checks.State == PullChecksState.Failing)
        {
            return "ciRed";
        }

        if (unhandled > 0)
        {
            return "comments";
        }

        var gating = docs is { Skipped: false, View.State: PullState.Open } ? docs.View : null;
        return DocsRed(gating) ? "docsRed" : Pending(pull, onHead, gating) ? "pending" : "green";
    }

    private static bool DocsRed(PullView? gating) =>
        gating is not null && string.Equals(gating.Checks.Sha, gating.HeadSha, StringComparison.Ordinal) && gating.Checks.State == PullChecksState.Failing;

    // Checks on an older commit (right after a push) or still running are not an answer yet; no check at all on the head is a repository without required checks.
    private static bool Pending(PullView pull, bool onHead, PullView? gating) =>
        !onHead || pull.Checks.State == PullChecksState.Pending || DocsPending(gating);

    private static bool DocsPending(PullView? docs) =>
        docs is not null && (docs.Checks.State == PullChecksState.Pending || !string.Equals(docs.Checks.Sha, docs.HeadSha, StringComparison.Ordinal));

    private JsonObject State(PullView pull, string outcome, Conversation conversation, DocsRead? docs)
    {
        var (threads, work, marked) = conversation;
        var pullState = pull.State.ToString().ToLowerInvariant();
        return new JsonObject
        {
            ["link"] = pull.Link,
            ["url"] = pull.Link,
            ["state"] = string.Equals(outcome, "green", StringComparison.Ordinal) ? "green" : pullState,
            ["pullState"] = pullState,
            ["headSha"] = pull.HeadSha,
            ["stale"] = !string.Equals(pull.Checks.Sha, pull.HeadSha, StringComparison.Ordinal),
            ["head"] = new JsonObject { ["sha"] = pull.HeadSha },
            ["mergeable"] = pull.Mergeable,
            ["reviewDecision"] = pull.ReviewDecision,
            ["unresolvedThreads"] = pull.UnresolvedThreads,
            ["checks"] = new JsonObject
            {
                ["state"] = pull.Checks.State.ToString().ToLowerInvariant(),
                ["sha"] = pull.Checks.Sha,
                ["failing"] = new JsonArray([.. pull.Checks.Failing.Select(static f => JsonValue.Create(f))]),
            },
            ["queue"] = new JsonObject
            {
                ["unhandled"] = work.Count,
                ["held"] = threads.Count(t => !t.Resolved && !Own(t) && marked.TryGetValue(t.Key, out var m) && string.Equals(m.Outcome, "held", StringComparison.Ordinal)),
                ["untrusted"] = work.Count(static t => !t.IsBot && !_trusted.Contains(t.Association, StringComparer.OrdinalIgnoreCase)),
                ["bots"] = work.Count(static t => t.IsBot),
            },
            ["comments"] = new JsonArray([.. threads.Select(t => (JsonNode)Comment(t, marked.GetValueOrDefault(t.Key)))]),
            ["docs"] = docs is null ? null : DocsState(docs.View),
            ["self"] = null,
            ["mergeBlockedBy"] = new JsonArray([.. BlockedBy(pull).Select(static b => JsonValue.Create(b))]),
            ["heldBlocksMerge"] = false,
            ["at"] = clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture),
        };
    }

    private static JsonObject DocsState(PullView docs) => new()
    {
        ["link"] = docs.Link,
        ["state"] = docs.State.ToString().ToLowerInvariant(),
        ["headSha"] = docs.HeadSha,
        ["checks"] = new JsonObject
        {
            ["state"] = docs.Checks.State.ToString().ToLowerInvariant(),
            ["sha"] = docs.Checks.Sha,
            ["failing"] = new JsonArray([.. docs.Checks.Failing.Select(static f => JsonValue.Create(f))]),
        },
    };

    private static IEnumerable<string> BlockedBy(PullView pull)
    {
        if (!string.Equals(pull.ReviewDecision, "APPROVED", StringComparison.Ordinal))
        {
            yield return "reviewRequired";
        }
    }

    private static JsonObject Comment(PullThread thread, PrCommentMark? mark) => new()
    {
        ["key"] = thread.Key,
        ["kind"] = thread.Kind.ToString().ToLowerInvariant(),
        ["author"] = thread.Author,
        ["association"] = thread.Association,
        ["isBot"] = thread.IsBot,
        ["resolved"] = thread.Resolved,
        ["outdated"] = thread.Outdated,
        ["path"] = thread.Path,
        ["body"] = thread.Body,
        ["mark"] = mark is null ? null : new JsonObject
        {
            ["outcome"] = mark.Outcome,
            ["reason"] = mark.Reason,
            ["commit"] = mark.Commit,
            ["at"] = mark.At.ToString("O", CultureInfo.InvariantCulture),
        },
        ["markers"] = new JsonArray([.. thread.Markers.Select(static m => JsonValue.Create(m))]),
    };

    // Sindri's replies never come back as work: a marker it wrote.
    internal static bool Own(PullThread thread) =>
        thread.Markers.Any(static m => m.StartsWith("sindri", StringComparison.Ordinal)) && SindriFixMarker.Item(thread.Markers) is null;

    private sealed record DocsRead(PullView View, bool Skipped);

    private sealed record Conversation(IReadOnlyList<PullThread> Threads, List<PullThread> Work, Dictionary<string, PrCommentMark> Marked);
}
