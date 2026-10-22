using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Pr;

/// <summary>
/// 'step.pr.snapshot' on the block runtime (babysitter spec §4.3): one forge read of the pull request and its conversation, reported as the first outcome that holds, in the
/// spec's order, with 'fact:prState' on every outcome and 'fact:mergedSha' when merged. The fact is the pull request view in camelCase (link, state, headSha, mergeable,
/// reviewDecision, unresolvedThreads, checks { state, sha, failing }), 'pullState' (open, closed or merged), 'comments' (the threads, oldest first, as the queue Steps parse
/// them), 'self' (null), 'queue' counts, 'mergeBlockedBy' and 'at'. 'state' is 'green' when the outcome is green (the merge gate reads it), else the pull request's own state.
/// Not read yet, for want of a port: the base branch (restack), the docs pull request (mergedDocsOpen, docsRed), the proven head (stale), the comment marks and the workspace.
/// </summary>
/// <param name="pulls">The forge, through Sindri's port.</param>
/// <param name="clock">The clock.</param>
internal sealed class PrSnapshotStep(IPullRequests pulls, TimeProvider clock) : IBlockStep, IBlockStepId
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

        var work = threads.Value.Where(static t => !t.Resolved && !Own(t)).ToList();   // the forge port has no login, so Sindri's replies are told apart by their markers alone
        var outcome = Outcome(pull.Value, work.Count);
        var state = State(pull.Value, outcome, threads.Value, work);
        return string.Equals(outcome, "merged", StringComparison.Ordinal)
            ? StepFacts.Done(outcome, ("fact:prState", state), ("fact:mergedSha", JsonValue.Create(pull.Value.HeadSha)))
            : StepFacts.Done(outcome, ("fact:prState", state));
    }

    private static string Outcome(PullView pull, int unhandled)
    {
        if (pull.State != PullState.Open)
        {
            return pull.State == PullState.Merged ? "merged" : "closed";
        }

        if (pull.Mergeable == false)
        {
            return "conflict";
        }

        var onHead = string.Equals(pull.Checks.Sha, pull.HeadSha, StringComparison.Ordinal);
        if (onHead && pull.Checks.State == PullChecksState.Failing)
        {
            return "ciRed";
        }

        if (unhandled > 0)
        {
            return "comments";
        }

        // Checks on an older commit (right after a push) or still running are not an answer yet; no check at all on the head is a repository without required checks.
        return !onHead || pull.Checks.State == PullChecksState.Pending ? "pending" : "green";
    }

    private JsonObject State(PullView pull, string outcome, IReadOnlyList<PullThread> threads, List<PullThread> work)
    {
        var pullState = pull.State.ToString().ToLowerInvariant();
        return new JsonObject
        {
            ["link"] = pull.Link,
            ["state"] = string.Equals(outcome, "green", StringComparison.Ordinal) ? "green" : pullState,
            ["pullState"] = pullState,
            ["headSha"] = pull.HeadSha,
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
                ["held"] = 0,
                ["untrusted"] = work.Count(static t => !t.IsBot && !_trusted.Contains(t.Association, StringComparer.OrdinalIgnoreCase)),
                ["bots"] = work.Count(static t => t.IsBot),
            },
            ["comments"] = new JsonArray([.. threads.Select(static t => (JsonNode)Comment(t))]),
            ["self"] = null,
            ["mergeBlockedBy"] = new JsonArray([.. BlockedBy(pull).Select(static b => JsonValue.Create(b))]),
            ["heldBlocksMerge"] = false,
            ["at"] = clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture),
        };
    }

    private static IEnumerable<string> BlockedBy(PullView pull)
    {
        if (!string.Equals(pull.ReviewDecision, "APPROVED", StringComparison.Ordinal))
        {
            yield return "reviewRequired";
        }
    }

    private static JsonObject Comment(PullThread thread) => new()
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
        ["markers"] = new JsonArray([.. thread.Markers.Select(static m => JsonValue.Create(m))]),
    };

    // Sindri's replies never come back as work: a marker it wrote.
    private static bool Own(PullThread thread) =>
        thread.Markers.Any(static m => m.StartsWith("sindri", StringComparison.Ordinal));
}
