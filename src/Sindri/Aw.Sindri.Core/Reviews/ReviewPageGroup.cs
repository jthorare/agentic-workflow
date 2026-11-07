namespace Aw.Sindri.Core.Reviews;

/// <summary>A group of files.</summary>
/// <param name="Name">The group's name.</param>
/// <param name="Folded">Whether it starts folded.</param>
/// <param name="Files">Its files.</param>
public sealed record ReviewPageGroup(
    string Name,
    bool Folded,
    IReadOnlyList<ReviewPageFile> Files);
