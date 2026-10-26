using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Scoping;

/// <summary>
/// 'step.scope.propose', on the block runtime (scoping spec §6.1, C9): submits the map's surfaces that have no node key as draft proposals, the run being the
/// instance's address (a resubmission replaces the run's proposals; the surfaces a first pass proposed are sent again, and an empty set sends nothing, since an empty list would withdraw them), and records each surface's graph status through
/// the desk code. It carries the relevance review's map fixes (fact:mapFixes) through with a status, 'recorded' after a proposal pass and 'skipped' otherwise; none are submitted, since the context engine takes new surfaces only.
/// With no repo for the item, or the context engine unable to answer, nothing is submitted and the Step reports
/// 'skipped' (statuses 'unknown'), which the stop shows. An item whose repo could not be read never gets here: the runner fails the Step first.
/// </summary>
/// <param name="proposals">The run's proposals on the context map.</param>
internal sealed class ScopeProposeStep(IScopeRunProposals proposals) : ScopingCodeStep(Id), IBlockStepId, IRepoStep
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.scope.propose");

    /// <inheritdoc/>
    protected override async ValueTask<Result<IReadOnlyDictionary<string, string>>> Extra(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var facts = new Dictionary<string, string>(StringComparer.Ordinal);
        if (context.Repo is not { } repo)
        {
            return Facts(facts);
        }

        if (!InstanceAddress.TryFrom(context.Address ?? string.Empty, out var address))
        {
            return SindriErrors.InvalidInput.With("the instance has no address to name the run's proposals by");
        }

        // The surfaces this run proposes: those with no node yet, and those a first pass proposed (status 'new', their node key already written), so a revise
        // sends the same set again and the run's earlier proposals stay. An empty set sends nothing: it would withdraw every live proposal of the run.
        var surfaces = ScopeJson.Items(Fact(context, "scopeMap"), "surfaces").Where(static s => ScopeJson.Str(s, "key") is null || string.Equals(ScopeJson.Str(s, "graphStatus"), "new", StringComparison.Ordinal))
            .Select(Surface).OfType<ProposedScopeSurface>().ToList();
        if (surfaces.Count == 0)
        {
            return Facts(facts);
        }

        var proposed = await proposals.Replace(address, repo, surfaces, cancellationToken).ConfigureAwait(false);
        if (!proposed.IsError)
        {
            facts["proposeResult"] = Answer(proposed.Value);
        }

        return Facts(facts);
    }

    private static ProposedScopeSurface? Surface(JsonNode surface) =>
        ScopeJson.Str(surface, "title") is { } title ? new ProposedScopeSurface(title, ScopeJson.Str(surface, "kind")) : null;

    // The answer 'context map propose --json' prints, which the desk code reads: each surface with its node key and status.
    private static string Answer(IReadOnlyList<ScopeSurfaceStatus> statuses) => ScopeJson.Write(new JsonObject
    {
        ["surfaces"] = new JsonArray([.. statuses.Select(static s => (JsonNode?)new JsonObject { ["surface"] = s.Surface, ["node"] = s.Node, ["status"] = s.Status })]),
    });
}
