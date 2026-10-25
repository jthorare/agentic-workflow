using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
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
/// <param name="launches">The launches view, for the 'runs' fact an engine run never stores (#1008); null reads none, so the runs stay as stored.</param>
/// <param name="packets">Hashes the assembled packet, which 'complete' records as fact:scopePacket for the owner's approval and the freeze.</param>
internal sealed class ScopeEvidenceStep(IRunArea area, IPackets packets, IRepoCheckouts checkouts, IRunDiff diff, ILaunchViews? launches = null) : ScopingCodeStep(Id), IBlockStepId, IRepoStep
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.scope.evidence");

    /// <summary>
    /// The commit of the checkout the scoping read, as the 'baseSha' fact the packet's scope map is stamped with (by code, never a model: the facts' own 'baseSha' is replaced).
    /// An item with no repo has no checkout, so its map carries none and the design station stops at intake.
    /// </summary>
    /// <param name="context">The Step's context.</param>
    /// <param name="cancellationToken">Cancelled when the run ends before the Step does.</param>
    /// <returns>The code's extra facts, or the error that fails the Step.</returns>
    protected override async ValueTask<Result<IReadOnlyDictionary<string, string>>> Extra(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var extra = new Dictionary<string, string>(StringComparer.Ordinal);
        if (context.Repo is { } repo)
        {
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
            extra["baseSha"] = ScopeJson.Write(sha);
        }

        // why: a stored 'runs' (the desk flow keeps one) is the record; only an engine run, which stores none, reads the launches
        if (launches is not null && !context.Facts.ContainsKey(FactKey.From("fact:runs")))
        {
            var runs = await ScopingRuns.Of(launches, context.Address, cancellationToken).ConfigureAwait(false);
            if (runs.IsError)
            {
                return runs.Error;
            }

            if (runs.Value is { } json)
            {
                extra["runs"] = json;
            }
        }

        return Facts(extra);
    }

    /// <inheritdoc/>
    protected override ValueTask<Result<bool>> Keep(StepContext context, ScopingDeskResponse response, CancellationToken cancellationToken) =>
        WriteFiles(area, context, response, cancellationToken);

    /// <inheritdoc/>
    protected override async ValueTask<Result<IReadOnlyDictionary<string, string>>> MoreOutputs(StepContext context, ScopingDeskResponse response, CancellationToken cancellationToken)
    {
        if (response.Outcome != "complete")
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
