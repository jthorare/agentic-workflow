using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.RemoveSessionWorktree;

/// <summary>
/// Remove the one-off worktree of an Ended session (DELETE /sindri/sessions/{actId}/worktree, 'sindri agents worktree remove'; agents spec §15.8; human only): 'git worktree remove' without --force, on the path recorded in the 'agent.op' event of the start.
/// A tree with changes, or one a start did not make, is SND-AGENT-018.
/// </summary>
/// <param name="ActId">The key the start used.</param>
public sealed record RemoveSessionWorktreeRequest(ActId ActId) : ICommand<RemovedWorktree>;
