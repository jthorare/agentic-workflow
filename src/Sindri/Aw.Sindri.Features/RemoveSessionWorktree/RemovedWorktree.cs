using Aw.Sindri.Core.Ids;
namespace Aw.Sindri.Features.RemoveSessionWorktree;

/// <summary>What the remove answers.</summary>
/// <param name="ActId">The key the start used.</param>
/// <param name="Path">The worktree folder that was removed.</param>
public sealed record RemovedWorktree(ActId ActId, string Path);
