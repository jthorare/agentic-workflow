using System.Text.Json.Nodes;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Pr;

/// <summary>
/// 'step.pr.ready-check' on the block runtime (PR babysitter spec §4.5): one forge read of the pull request, compared with the snapshot the instance last recorded
/// as 'fact:prState'. It decides from facts only: 'same' when the state, head, checks (state and the commit they ran on), mergeable flag, review decision and
/// open thread count all match, which returns to 'ready' without a notification; 'changed' otherwise, which re-snapshots. A snapshot it can't read counts as changed.
/// It writes no fact. A forge that can't be read fails the Step, which the recipe also routes to 'snapshot'.
/// </summary>
/// <param name="pulls">The forge, through Sindri's port.</param>
internal sealed class PrReadyCheckStep(IPullRequests pulls) : IBlockStep, IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.pr.ready-check");

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (StepFacts.Text(context, "fact:pr") is not { Length: > 0 } link)
        {
            return BlockStepResult.Failed("step.pr.ready-check needs fact:pr (the pull request's link)");
        }

        var now = await pulls.Get(link, cancellationToken).ConfigureAwait(false);
        if (now.IsError)
        {
            return BlockStepResult.Failed($"step.pr.ready-check can't read {link}: {now.Error.Message}");
        }

        var then = Snapshot.Read(StepFacts.Read(context, "fact:prState"));
        return StepFacts.Done(then is not null && Snapshot.Matches(then, now.Value) ? "same" : "changed");
    }

    /// <summary>The one place that reads the snapshot fact's keys (camelCase of the pull request view: link, pullState (the pull request's own state; the older snapshot kept it in state, which now holds the outcome word), headSha, mergeable, reviewDecision, unresolvedThreads, checks { state, sha, failing }).</summary>
    private static class Snapshot
    {
        /// <summary>What the babysitter waits on: everything that moves the snapshot's outcome or the ready view.</summary>
        internal static bool Matches(PullView then, PullView now) =>
            then.State == now.State
            && string.Equals(then.HeadSha, now.HeadSha, StringComparison.Ordinal)
            && then.Mergeable == now.Mergeable
            && string.Equals(then.ReviewDecision, now.ReviewDecision, StringComparison.Ordinal)
            && then.UnresolvedThreads == now.UnresolvedThreads
            && then.Checks.State == now.Checks.State
            && string.Equals(then.Checks.Sha, now.Checks.Sha, StringComparison.Ordinal);

        /// <summary>The recorded snapshot as a pull request view; null when it isn't one.</summary>
        internal static PullView? Read(JsonNode? fact)
        {
            if (fact is not JsonObject root || root["checks"] is not JsonObject checks
                || Word<PullState>(root["pullState"] ?? root["state"]) is not { } state || Word<PullChecksState>(checks["state"]) is not { } checksState
                || Text(root["headSha"]) is not { } head || Text(checks["sha"]) is not { } checksSha)
            {
                return null;
            }

            return new PullView(
                Text(root["link"]) ?? string.Empty,
                state,
                head,
                Flag(root["mergeable"]),
                Text(root["reviewDecision"]),
                Count(root["unresolvedThreads"]),
                new PullChecks(checksState, checksSha, []),
                Labels(root["labels"]),
                Text(root["base"]) ?? string.Empty);
        }

        private static string[] Labels(JsonNode? node) => node is JsonArray names ? [.. names.Select(Text).OfType<string>()] : [];

        private static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

        private static bool? Flag(JsonNode? node) => node is JsonValue value && value.TryGetValue<bool>(out var flag) ? flag : null;

        private static int Count(JsonNode? node) => node is JsonValue value && value.TryGetValue<int>(out var count) ? count : 0;

        // An enum as its name (any case) or its number.
        private static T? Word<T>(JsonNode? node)
            where T : struct, Enum
        {
            if (node is not JsonValue value)
            {
                return null;
            }

            if (value.TryGetValue<string>(out var name))
            {
                return Enum.TryParse<T>(name, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed) ? parsed : null;
            }

            return value.TryGetValue<int>(out var number) && Enum.IsDefined(typeof(T), number) ? (T)Enum.ToObject(typeof(T), number) : null;
        }
    }
}
