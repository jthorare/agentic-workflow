namespace Aw.Forge.Core.Pulls;

/// <summary>One file a pull request changes.</summary>
/// <param name="Path">The path at the head.</param>
/// <param name="PreviousPath">The path before a rename or copy; null otherwise.</param>
/// <param name="Status">What the pull request did to the file.</param>
/// <param name="Additions">Lines added.</param>
/// <param name="Deletions">Lines deleted.</param>
/// <param name="Patch">The file's diff; null for a binary file or one the forge left out as too large. Untrusted text.</param>
public sealed record PullFile(string Path, string? PreviousPath, PullFileStatus Status, int Additions, int Deletions, string? Patch);
