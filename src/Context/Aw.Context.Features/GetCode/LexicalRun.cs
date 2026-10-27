using System.Collections.Concurrent;
using Aw.Context.Core.Engine;

namespace Aw.Context.Features.GetCode;

/// <summary>One repo at one commit within a pass: what to search, the tracked files that may be searched, and the texts already read.</summary>
/// <param name="pass">The pass.</param>
/// <param name="repo">The repo.</param>
/// <param name="commit">The full commit searched.</param>
/// <param name="tracked">The repo's tracked files at the commit, never a denied path.</param>
internal sealed class LexicalRun(LexicalPass pass, ContextRepo repo, string commit, IReadOnlyList<string> tracked)
{
    internal LexicalPass Pass { get; } = pass;

    internal ContextRepo Repo { get; } = repo;

    internal string Commit { get; } = commit;

    internal IReadOnlyList<string> Tracked { get; } = tracked;

    internal ConcurrentDictionary<string, string?> Texts { get; } = new(StringComparer.Ordinal);
}
