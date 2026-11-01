using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Map;

/// <summary>What a node's source points at, before any link is looked up: a stored record, or a file of a repo (#1702).</summary>
public sealed class SourceTarget
{
    private SourceTarget()
    {
    }

    /// <summary>The stored record, or null for a file.</summary>
    public RecordKey? Record { get; private init; }

    /// <summary>The repo the file is in (null when the node belongs to no single repo and the map has none).</summary>
    public string? Repo { get; private init; }

    /// <summary>The file's path in the repo, or null for a record.</summary>
    public string? Path { get; private init; }

    /// <summary>A target that is a stored record.</summary>
    /// <param name="record">The record's key.</param>
    /// <returns>The target.</returns>
    public static SourceTarget OfRecord(RecordKey record) => new() { Record = record };

    /// <summary>A target that is a file.</summary>
    /// <param name="repo">The repo, or null.</param>
    /// <param name="path">The path in the repo.</param>
    /// <returns>The target.</returns>
    public static SourceTarget OfFile(string? repo, string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return new SourceTarget { Repo = repo, Path = path };
    }
}
