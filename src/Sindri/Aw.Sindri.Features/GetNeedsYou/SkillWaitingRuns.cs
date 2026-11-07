using Aw.Sindri.Core.Launches;
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
    public ValueTask<IReadOnlyList<SkillWaitingRun>> Find(CancellationToken cancellationToken)
    {
        _ = (liveness, views, instances, registry);   // F-2a: not read yet
        return ValueTask.FromResult<IReadOnlyList<SkillWaitingRun>>([]);
    }
}
