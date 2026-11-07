namespace Aw.Sindri.Core.Reviews;

/// <summary>One file of the diff.</summary>
/// <param name="Path">The path.</param>
/// <param name="Status">added, modified, renamed, deleted or changed.</param>
/// <param name="PreviousPath">A rename's old path.</param>
/// <param name="Similarity">A rename's percent.</param>
/// <param name="Additions">Lines added.</param>
/// <param name="Deletions">Lines removed.</param>
/// <param name="Patch">The unified patch; null when held back.</param>
/// <param name="Omitted">tooLarge, forgeOmitted, binary or deleted.</param>
/// <param name="Note">The words for a rename or a held-back file.</param>
/// <param name="Url">The file on the forge.</param>
public sealed record ReviewPageFile(
    string Path,
    string Status,
    string? PreviousPath,
    int? Similarity,
    int Additions,
    int Deletions,
    string? Patch,
    string? Omitted,
    string? Note,
    string Url);
