namespace Aw.Sindri.Core.Projects.TruthSources;

/// <summary>What a project's truth is derived from (issue 3718).</summary>
/// <param name="Repos">owner/name of the repos whose open PRs and agents count.</param>
/// <param name="PullUrls">PR links that count even when their repo is not in <paramref name="Repos"/>.</param>
/// <param name="Branches">Branches and branch prefixes (a github.com/&lt;owner&gt;/&lt;repo&gt;/tree/&lt;branch&gt; link, or the branch search github.com/&lt;owner&gt;/&lt;repo&gt;/branches/all?query=&lt;prefix&gt;) whose PRs count even when their repo is not in <paramref name="Repos"/> (#4333); null is none.</param>
public sealed record ProjectSources(IReadOnlyList<string> Repos, IReadOnlyList<string> PullUrls, IReadOnlyList<BranchSource>? Branches = null);
