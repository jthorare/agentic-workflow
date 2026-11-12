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
/// <param name="connections">The connection store the trigger names are checked against, or null (not read yet).</param>
/// <param name="memories">The memory store the memory block names are checked against, or null (not read yet).</param>
internal sealed class ProfileWorkflowChecker(IApprovedProfile profile, IGraphBlockCatalog blocks, ICodeCheck? codeCheck = null, IConnectionStore? connections = null, IMemoryStore? memories = null) : IWorkflowChecker
{
    /// <summary>The connection store the names are checked against; kept for the fix, not read yet.</summary>
    internal IConnectionStore? Connections { get; } = connections;

    /// <summary>The memory store the names are checked against; kept for the fix, not read yet.</summary>
    internal IMemoryStore? Memories { get; } = memories;

    public async ValueTask<GraphValidator> Validator(CancellationToken cancellationToken)
    {
        var world = await ProfileGraphWorld.Load(profile, cancellationToken).ConfigureAwait(false);
        var context = new GraphValidationContext(blocks.Types, world, (cron, zone) => CronExpression.TryParse(cron, zone, out _, out var problem) ? null : problem, ParseCondition)
        {
            BoxCap = await BoxCap(cancellationToken).ConfigureAwait(false),
            CodeCheck = codeCheck is null ? null : body => codeCheck.Check(body, cancellationToken),
        };
        return new GraphValidator(context);
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
