namespace Aw.Sindri.Core;

/// <summary>One code file of a pin.</summary>
/// <param name="Repo">The repo's name.</param>
/// <param name="Path">The repo-relative path.</param>
public sealed record PinnedFile(string Repo, string Path);
