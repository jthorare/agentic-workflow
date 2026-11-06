namespace Aw.Forge.Core.Pulls;

/// <summary>A changed file as a fake or file forge is told about it.</summary>
/// <param name="File">The file as Files reports it.</param>
/// <param name="Text">The file's text at the head, or null for a binary or removed file (FileText then answers not found).</param>
/// <param name="BaseText">The file's text at the base (under <see cref="PullFile.PreviousPath"/> for a rename), or null.</param>
public sealed record SeedFile(PullFile File, string? Text, string? BaseText = null);
