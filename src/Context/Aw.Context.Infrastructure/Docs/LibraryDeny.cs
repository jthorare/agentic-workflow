namespace Aw.Context.Infrastructure.Docs;

/// <summary>What the library never reads (review page spec 2026-11-04 §5.1): the built-in deny globs on every root, then the root's own. Dot folders are the walker's.</summary>
internal sealed class LibraryDeny(IReadOnlyList<string> own)
{
    private static readonly string[] _builtin = ["**/.env*", "**/*.pem", "**/*.key", "**/id_*", "**/*.p12", "**/secrets/**", "**/.git/**"];

    private readonly Glob _globs = new([.. _builtin, .. own]);

    /// <summary>True when <paramref name="relative"/> is denied.</summary>
    /// <param name="relative">The root-relative path, '/'-separated.</param>
    /// <returns>Whether it is denied.</returns>
    public bool Denies(string relative) => _globs.Matches(relative);
}
