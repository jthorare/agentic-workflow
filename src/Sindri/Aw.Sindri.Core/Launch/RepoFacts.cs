using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Launch;

/// <summary>One profile repo in 'launch.probed' (UI spec E-3).</summary>
/// <param name="Repo">The repo.</param>
/// <param name="Present">True when its path is a git checkout on this box.</param>
public sealed record RepoFacts(RepoName Repo, bool Present);
