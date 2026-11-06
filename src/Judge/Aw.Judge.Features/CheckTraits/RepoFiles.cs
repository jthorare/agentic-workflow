namespace Aw.Judge.Features.CheckTraits;

/// <summary>A repo (its top-level directory, as git prints it) and the files of it a check is about, repo-relative (none for a stop or a scan).</summary>
/// <param name="Root">The repo's top-level directory.</param>
/// <param name="Files">The edited files.</param>
internal sealed record RepoFiles(string Root, List<string> Files);
