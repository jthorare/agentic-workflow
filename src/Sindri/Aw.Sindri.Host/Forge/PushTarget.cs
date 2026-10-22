using Aw.Forge.Core.Refs;

namespace Aw.Sindri.Host.Forge;

/// <summary>Where and what <see cref="ForgePullRequests"/> pushes: the repository's ref, the worktree, the commit, the branch and the lease.</summary>
internal sealed record PushTarget(ForgeRef Pull, string Workspace, string Sha, string Branch, string? LeaseSha);
