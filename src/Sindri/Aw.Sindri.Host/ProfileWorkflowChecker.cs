using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Expressions;
using Aw.Sindri.Core.Graph.Schedule;
using Aw.Sindri.Core.Graph.Validation;
using Aw.Sindri.Core.Memory;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Resources;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Host;

/// <summary>The validator the workflow write slices use: the block catalog, the repos of the approved profile (when one can be read) and the cron and condition parsers; the same context 'wf validate' builds.</summary>
/// <param name="profile">The approved profile.</param>
/// <param name="blocks">The graph block catalog.</param>
/// <param name="codeCheck">Compiles a 'code' node's body, or null to skip the check.</param>
/// <param name="connections">The connection store the trigger names are checked against, or null when there is no ledger.</param>
/// <param name="memories">The memory store the memory block names are checked against, or null when there is no ledger.</param>
internal sealed class ProfileWorkflowChecker(IApprovedProfile profile, IGraphBlockCatalog blocks, ICodeCheck? codeCheck = null, IConnectionStore? connections = null, IMemoryStore? memories = null) : IWorkflowChecker
{
    public async ValueTask<GraphValidator> Validator(CancellationToken cancellationToken)
    {
        var world = await ProfileGraphWorld.Load(profile, connections, memories, cancellationToken).ConfigureAwait(false);
        var context = new GraphValidationContext(blocks.Types, world, (cron, zone) => CronExpression.TryParse(cron, zone, out _, out var problem) ? null : problem, ParseCondition)
        {
            BoxCap = await BoxCap(cancellationToken).ConfigureAwait(false),
            AgentTarget = await AgentTarget(cancellationToken).ConfigureAwait(false),
            CodeCheck = codeCheck is null ? null : body => codeCheck.Check(body, cancellationToken),
        };
        return new GraphValidator(context);
    }

    /// <summary>Whether the profile sets 'workflow.agent.target' (#2986): Unavailable when no profile can be read.</summary>
    private async ValueTask<GraphLookup> AgentTarget(CancellationToken cancellationToken)
    {
        var latest = await profile.Latest(cancellationToken).ConfigureAwait(false);
        return latest.IsSuccess && latest.Value is { } loaded
            ? loaded.Profile.Workflow?.Agent?.Target is null ? GraphLookup.Unknown : GraphLookup.Known
            : GraphLookup.Unavailable;
    }

    /// <summary>The cap from the profile's 'sandbox.limits.memory' (#2531); null when the profile sets none, or sets 'auto' (the box's RAM decides that at launch). The profile has no wall limit, so the wall is unbounded here.</summary>
    private async ValueTask<ResourceBoxCap?> BoxCap(CancellationToken cancellationToken)
    {
        var latest = await profile.Latest(cancellationToken).ConfigureAwait(false);
        var memory = latest.IsSuccess ? latest.Value?.Profile.Sandbox?.Limits?.Memory : null;
        return memory is null || string.Equals(memory, SandboxDefaults.Memory, StringComparison.Ordinal) || !SandboxUnits.TryParseSize(memory, out var bytes)
            ? null   // why: a size the profile rules would refuse caps nothing here; the profile check reports it
            : new ResourceBoxCap(bytes, TimeSpan.MaxValue);
    }

    private static ConditionParse ParseCondition(string text) =>
        ConditionExpression.TryParse(text, out var expression, out var error) ? new ConditionParse(Error: null, expression!.References) : new ConditionParse(error, []);   // why: TryParse sets the expression when it returns true
}
