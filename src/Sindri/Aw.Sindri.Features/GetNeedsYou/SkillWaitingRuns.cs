using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Features.GetNeedsYou;

/// <summary>
/// The skill block runs waiting on the owner (skill flows spec §7: the row is computed live, the way the stalled agent runs feeds the stalled rows). A tracked skill launch
/// counts while its launch row holds a 'launch.owner-waiting' no 'launch.turn-started' has answered, its instance is not taken over and is still in the Step's state; a read that fails leaves that run off.
/// </summary>
/// <param name="liveness">The launches this process tracks.</param>
/// <param name="views">The 'launches' view: the skill, the Step and the last owner-waiting of a launch.</param>
/// <param name="instances">Reads the instance, to see whether a person took it over and whether the Step is still open.</param>
/// <param name="registry">Reads the instance's pinned recipe, for the Step's display name.</param>
internal sealed class SkillWaitingRuns(LaunchLiveness liveness, ILaunchViews views, IInstanceReader instances, RecipeRegistryReader registry) : ISkillWaitingRuns
{
    public async ValueTask<IReadOnlyList<SkillWaitingRun>> Find(CancellationToken cancellationToken)
    {
        var found = new List<SkillWaitingRun>();
        foreach (var launch in liveness.Active())
        {
            if (await Run(launch, cancellationToken).ConfigureAwait(false) is { } run)
            {
                found.Add(run);
            }
        }

        return found;
    }

    private async ValueTask<SkillWaitingRun?> Run(LaunchId launch, CancellationToken cancellationToken)
    {
        try
        {
            var row = await views.Get(launch, cancellationToken).ConfigureAwait(false);
            if (row.IsError || row.Value is not { Live: true, Skill: { } skill, OwnerWaiting: { } waiting, OwnerWaitingAt: { } since, Step: { Instance: { } address, State: { } state } } known)
            {
                return null;
            }

            var instance = await instances.Get(address, cancellationToken).ConfigureAwait(false);
            if (instance.IsError || instance.Value.TakenOverBy is not null || instance.Value.Outcome is not null || !string.Equals(instance.Value.State, state, StringComparison.Ordinal))
            {
                return null;   // taken over, ended, or moved on from this Step
            }

            return new SkillWaitingRun(launch, known.Item, address, await StateName(instance.Value, state, cancellationToken).ConfigureAwait(false), skill, waiting, since);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return null;   // boundary: a read that fails leaves this run off, never the list
        }
    }

    // The recipe's display name for the Step; the state id when the recipe can't be read.
    private async ValueTask<string> StateName(InstanceView view, string state, CancellationToken cancellationToken)
    {
        var recipe = await registry.Recipe(view.Recipe.Hash, cancellationToken).ConfigureAwait(false);
        return !recipe.IsError && recipe.Value is { } read && read.States.TryGetValue(state, out var found) && !string.IsNullOrWhiteSpace(found.DisplayName) ? found.DisplayName : state;
    }
}
