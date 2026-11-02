using System.Text.Json.Nodes;
using Aw.Context.Core.Facade;
using Aw.Context.Core.ScopeGraph;
using Aw.Kernel;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Scoping;

/// <summary>
/// 'step.scope.evidence', on the block runtime (scoping spec §8.2, SC7): <c language="text">ScopingSteps.Evidence</c>'s code behind <see cref="IBlockStep"/>. Every check runs over the facts,
/// and on 'complete' the packet is assembled in the instance's run area as the desk assembles it, so the owner's stop reads files, not a claim. A run area that cannot be written fails the Step.
/// </summary>
/// <param name="area">The instance's run area.</param>
/// <param name="checkouts">Where the item's repo checkout is.</param>
/// <param name="diff">Names the checkout's head, the scope map's baseSha.</param>
/// <param name="packets">Hashes the assembled packet, which 'complete' records as fact:scopePacket for the owner's approval and the freeze.</param>
/// <param name="runs">The instance's agent runs from the ledger, which the model checks compare and 'complete' records as fact:runs.</param>
/// <param name="engine">The context engine, for the graph calls each agent run made (issue 1005); none leaves them out of fact:runs.</param>
internal sealed class ScopeEvidenceStep(IRunArea area, IPackets packets, IRepoCheckouts checkouts, IRunDiff diff, IScopeRunRecords runs, IContextEngine? engine = null) : ScopingCodeStep(Id), IBlockStepId, IRepoStep
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.scope.evidence");

    /// <summary>
    /// The agent runs of the instance, as the 'runs' fact (from the ledger, never a model: a 'runs' fact already held is replaced), and the commit of the checkout the scoping read,
    /// as the 'baseSha' fact the packet's scope map is stamped with (by code, never a model: the facts' own 'baseSha' is replaced).
    /// An item with no repo has no checkout, so its map carries none and the design station stops at intake.
    /// </summary>
    /// <param name="context">The Step's context.</param>
    /// <param name="cancellationToken">Cancelled when the run ends before the Step does.</param>
    /// <returns>The code's extra facts, or the error that fails the Step.</returns>
    protected override async ValueTask<Result<IReadOnlyDictionary<string, string>>> Extra(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var recorded = context.Address is { } text && InstanceAddress.TryFrom(text, out var address)
            ? await runs.Of(address, cancellationToken).ConfigureAwait(false)
            : Results.Success<IReadOnlyList<ScopeRunRecord>>([]);
        if (recorded.IsError)
        {
            return recorded.Error;
        }

        var facts = new Dictionary<string, string>(StringComparer.Ordinal) { ["runs"] = ScopeJson.Write(ScopeRunFacts.Of(recorded.Value, await GraphReads(context.Address, recorded.Value, cancellationToken).ConfigureAwait(false))) };
        if (context.Repo is not { } repo)
        {
            return Facts(facts);
        }

        var root = await checkouts.Of(repo, cancellationToken).ConfigureAwait(false);
        if (root.IsError)
        {
            return root.Error;
        }

        var head = await diff.Head(root.Value, cancellationToken).ConfigureAwait(false);
        if (head.IsError)
        {
            return head.Error;
        }

        var sha = JsonValue.Create(head.Value) ?? throw new InvalidOperationException("a string is always a value"); // why: Create returns null only for a null string, and a head is not null
        facts["baseSha"] = ScopeJson.Write(sha);
        return Facts(facts);
    }

    /// <inheritdoc/>
    protected override ValueTask<Result<bool>> Keep(StepContext context, ScopingDeskResponse response, CancellationToken cancellationToken) =>
        WriteFiles(area, context, response, cancellationToken);

    // The graph calls each launched run's session made, from the context engine's log (issue 1005). A run the log can't answer for is left out, so its record says nothing rather than zero.
    private async ValueTask<Dictionary<string, IReadOnlyList<GraphRead>>> GraphReads(string? address, IReadOnlyList<ScopeRunRecord> records, CancellationToken cancellationToken)
    {
        var reads = new Dictionary<string, IReadOnlyList<GraphRead>>(StringComparer.Ordinal);
        if (engine is null || address is null)
        {
            return reads;
        }

        foreach (var token in records.Where(static r => string.Equals(r.Via, "agent", StringComparison.Ordinal)).Select(static r => r.RunToken).OfType<string>().Distinct(StringComparer.Ordinal))
        {
            var made = await engine.GraphReads(address, token, cancellationToken).ConfigureAwait(false);
            if (!made.IsError)
            {
                reads[token] = made.Value;
            }
        }

        return reads;
    }

    /// <inheritdoc/>
    protected override async ValueTask<Result<IReadOnlyDictionary<string, string>>> MoreOutputs(StepContext context, ScopingDeskResponse response, CancellationToken cancellationToken)
    {
        if (response.Outcome is not ("complete" or "converged"))
        {
            return Facts([]);
        }

        if (context.Address is not { } address)
        {
            return WorkflowErrors.BlockRunAreaUnavailable.With("the Step has no instance address, so there is no scope packet to hash");
        }

        var dir = area.PathFor(address, "scope-packet");
        if (dir.IsError)
        {
            return dir.Error;
        }

        var hash = await packets.Hash(new PacketRef(PacketKind.Scoping, dir.Value), cancellationToken).ConfigureAwait(false);
        return hash.IsError
            ? hash.Error
            : Facts(new Dictionary<string, string>(StringComparer.Ordinal) { ["scopePacket"] = new JsonObject { ["hash"] = hash.Value }.ToJsonString() });
    }
}
