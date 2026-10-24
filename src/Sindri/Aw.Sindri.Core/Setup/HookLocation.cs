namespace Aw.Sindri.Core.Setup;

/// <summary>Where git says a repo's pre-commit hook lives (core.hooksPath respected).</summary>
/// <param name="Path">The absolute path of the hook file, which may not exist yet.</param>
/// <param name="InGitDir">True when it is inside the repo's own git directory; false when core.hooksPath points elsewhere, a directory Sindri did not create.</param>
public sealed record HookLocation(string Path, bool InGitDir);
