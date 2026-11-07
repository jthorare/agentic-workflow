namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>One file of the diff.</summary>
/// <param name="Path">The path.</param>
/// <param name="Status">added, modified, renamed or deleted.</param>
/// <param name="PreviousPath">The old path of a rename; null otherwise.</param>
/// <param name="Similarity">A rename's percent; null otherwise.</param>
/// <param name="Additions">Lines added.</param>
/// <param name="Deletions">Lines removed.</param>
/// <param name="Patch">The unified patch; null when GitHub left it out.</param>
/// <param name="Omitted">deleted, binary or tooLarge when the contents are held back; null otherwise.</param>
/// <param name="Note">The words for a rename or a held-back file; null for none.</param>
/// <param name="Url">The file on GitHub.</param>
internal sealed record ReviewDiffFileView(
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
