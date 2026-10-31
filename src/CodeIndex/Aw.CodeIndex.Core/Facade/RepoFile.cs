using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Core.Facade;

/// <summary>One file of one repo: the member of <see cref="SymbolQuery.Within"/>, so a same-named file in another repo never passes.</summary>
/// <param name="Repo">The repo.</param>
/// <param name="Path">The repo-relative path.</param>
public readonly record struct RepoFile(RepoName Repo, string Path);
