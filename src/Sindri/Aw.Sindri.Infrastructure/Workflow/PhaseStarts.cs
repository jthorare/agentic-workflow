using Aw.Kernel;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>
/// <see cref="IPhaseStarts"/> over the instance runtime (line spec LN-WF1): the recipe is the one the line's approval pinned (a linked recipe is pinned and approved
/// with its parent, as a child is), and the instance is started at '{line}/phase.{id}.{attempt}' with the line as its parent link. The address is the workflow id,
/// and Start finds a running execution under it, so a repeat reports <see cref="PhaseStarted.Existed"/>.
/// </summary>
/// <param name="runtime">Reads the line and starts the linked instance; resolved on first use, since a box without the workflow engine has no instance runtime.</param>
/// <param name="approvals">The line's approval and its pins.</param>
/// <param name="recipes">The registered recipes, by the pinned hash.</param>
/// <param name="clock">When the instance starts.</param>
internal sealed class PhaseStarts(Func<IInstanceRuntime> runtime, IRecipeApprovals approvals, IRecipeStore recipes, TimeProvider clock) : IPhaseStarts
{
    private const string Tenant = "local";

    /// <inheritdoc/>
    public async ValueTask<Result<PhaseStarted>> Start(PhaseStart start, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(start);
        var address = start.Line.Linked(start.Recipe, start.Phase, start.Attempt);
        var line = await runtime().Get(start.Line, cancellationToken).ConfigureAwait(false);
        if (line.IsError)
        {
            return line.Error;
        }

        var pinned = await Pinned(line.Value.Recipe, start, cancellationToken).ConfigureAwait(false);
        if (pinned.IsError)
        {
            return pinned.Error;
        }

        var existing = await runtime().Get(address, cancellationToken).ConfigureAwait(false);
        var existed = existing.IsSuccess;   // why: an instance that answers a Get is started already; a missing one answers InstanceNotFound and is started below
        var input = new InstanceStart(
            pinned.Value,
            start.Item,
            start.Attempt,
            start.Inputs.ToDictionary(static f => f.Key, static f => new InputFact(f.Key.Value, f.Value)),
            Tenant,
            Ring: 0)
        {
            ChildAddress = address,
            ParentId = WorkflowInstanceId.Of(start.Line),
            StartedAt = clock.GetUtcNow(),
        };
        var started = await runtime().Start(input, cancellationToken).ConfigureAwait(false);
        return started.IsError ? started.Error : new PhaseStarted(address, existed);
    }

    /// <summary>The recipe at the hash the line's approval pinned.</summary>
    private async ValueTask<Result<RecipeVersion>> Pinned(RecipeVersion line, PhaseStart start, CancellationToken cancellationToken)
    {
        var approval = await approvals.Latest(line.Id, line.Version, line.Hash, cancellationToken).ConfigureAwait(false);
        if (approval.IsError)
        {
            return approval.Error;
        }

        if (approval.Value?.Pins.Children.FirstOrDefault(p => p.Recipe == start.Recipe) is not { } pin)
        {
            return WorkflowErrors.RecipeNotApproved.With($"{line.Id.Value} didn't pin {start.Recipe.Value} (fix: approve {start.Recipe.Value}, then {line.Id.Value} again)");
        }

        var registered = await recipes.Find(pin.Hash, cancellationToken).ConfigureAwait(false);
        if (registered.IsError)
        {
            return registered.Error;
        }

        return registered.Value is { } found
            ? new RecipeVersion(found.Id, found.Version, found.Hash)
            : WorkflowErrors.RecipeNotApproved.With($"{start.Recipe.Value}@{pin.Hash.Display} is pinned but not registered");
    }
}
