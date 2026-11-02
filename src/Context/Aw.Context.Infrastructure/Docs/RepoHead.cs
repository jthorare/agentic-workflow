using Aw.Context.Core.Engine;

namespace Aw.Context.Infrastructure.Docs;

/// <summary>One repo at its head: the commit and every blob in the tree.</summary>
/// <param name="Repo">The repo.</param>
/// <param name="Head">The head commit.</param>
/// <param name="Tree">Path to blob.</param>
internal sealed record RepoHead(ContextRepo Repo, string Head, IReadOnlyDictionary<string, string> Tree);
