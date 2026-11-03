namespace Aw.Judge.Core.Images;

/// <summary>The images a question's input names: where to read them and what their real paths must stay under.</summary>
public sealed record ImageRequest
{
    /// <summary>The paths in the order the question lists them: absolute, or relative to <see cref="Root"/> when it is set.</summary>
    public required IReadOnlyList<string> Paths { get; init; }

    /// <summary>The absolute directory every file's real path (symlinks resolved) must stay under, and that relative paths are taken from; null for no such limit.</summary>
    public string? Root { get; init; }
}
