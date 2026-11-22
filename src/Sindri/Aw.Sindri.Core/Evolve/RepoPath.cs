using System.Diagnostics.CodeAnalysis;

namespace Aw.Sindri.Core.Evolve;

/// <summary>A file path a model named, checked before any decision reads it.</summary>
[SuppressMessage("Meziantou.Analyzer", "MA0182:Internal type is apparently never used", Justification = "A stub until the next commit, where the tier rules and leak layers call it.")]
internal static class RepoPath
{
    /// <summary>Normalizes a repo-relative path: drops './' and repeated '/'.</summary>
    /// <param name="path">The path as named.</param>
    /// <returns>The normalized path, or null for an empty, absolute, '..', backslash or otherwise unsafe path.</returns>
    public static string? Normalize(string path) => throw new NotSupportedException();
}
