using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Expressions;
using Aw.Sindri.Core.Graph.Schedule;
using Aw.Sindri.Core.Graph.Validation;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Host.Commands;

/// <summary>The validator the workflow write slices use: the block catalog, the repos of the approved profile (when one can be read) and the cron and condition parsers; the same context 'wf validate' builds.</summary>
/// <param name="profile">The approved profile.</param>
/// <param name="blocks">The graph block catalog.</param>
internal sealed class ProfileWorkflowChecker(IApprovedProfile profile, IGraphBlockCatalog blocks) : IWorkflowChecker
{
    public async ValueTask<GraphValidator> Validator(CancellationToken cancellationToken)
    {
        var world = await ProfileGraphWorld.Load(profile, cancellationToken).ConfigureAwait(false);
        return new GraphValidator(new GraphValidationContext(blocks.Types, world, (cron, zone) => CronExpression.TryParse(cron, zone, out _, out var problem) ? null : problem, ParseCondition));
    }

    private static ConditionParse ParseCondition(string text) =>
        ConditionExpression.TryParse(text, out var expression, out var error) ? new ConditionParse(Error: null, expression!.References) : new ConditionParse(error, []);   // why: TryParse sets the expression when it returns true
}
