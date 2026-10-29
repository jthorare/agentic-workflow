using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Features.GetInstance;

/// <summary>Builds the detail view's context at the time of the read: the 'asOf' is that time, the recipe carries its display name (setup-once spec §7.7), and the open run's session and the reviewed facts' targets are joined in (q65; none without <paramref name="joins"/>).</summary>
/// <param name="clock">The time the status text and the 'asOf' are read at.</param>
/// <param name="names">The recipes' display names; null serves none.</param>
/// <param name="joins">The read-time joins; null serves neither.</param>
internal sealed class DetailContexts(TimeProvider clock, IRecipeDisplayNames? names = null, InstanceJoins? joins = null)
{
    public async ValueTask<InstanceDetailContext> Of(InstanceView view, DetailInputs inputs, CancellationToken cancellationToken)
    {
        var session = joins is null ? null : await joins.Session(view, cancellationToken).ConfigureAwait(false);
        var targets = joins is null ? null : await joins.ReviewTargets(view, cancellationToken).ConfigureAwait(false);
        var waiting = joins is null ? null : await joins.Waiting(view, cancellationToken).ConfigureAwait(false);
        return new InstanceDetailContext(inputs.Title, inputs.Parent, inputs.Counts, clock.GetUtcNow(), inputs.StepRuns, names?.Of(view.Recipe.Id), session, targets) { Waiting = waiting };
    }
}
