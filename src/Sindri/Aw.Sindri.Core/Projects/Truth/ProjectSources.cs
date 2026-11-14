namespace Aw.Sindri.Core.Projects.Truth;

/// <summary>What a project's truth is derived from (issue 3718).</summary>
/// <param name="Repos">owner/name of the repos whose open PRs and agents count.</param>
/// <param name="PullUrls">PR links that count even when their repo is not in <paramref name="Repos"/>.</param>
public sealed record ProjectSources(IReadOnlyList<string> Repos, IReadOnlyList<string> PullUrls);
