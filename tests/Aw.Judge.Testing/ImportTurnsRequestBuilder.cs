using Aw.Judge.Features.ImportTurns;

namespace Aw.Judge.Testing;

/// <summary>Builds an ImportTurnsRequest with deterministic defaults: one transcript, every age.</summary>
public sealed class ImportTurnsRequestBuilder
{
    private IReadOnlyList<string> _files = ["session-1.jsonl"];
    private TimeSpan? _since;

    /// <summary>Sets the transcript files.</summary>
    /// <param name="files">The paths.</param>
    /// <returns>This builder.</returns>
    public ImportTurnsRequestBuilder Files(params string[] files)
    {
        _files = files;
        return this;
    }

    /// <summary>Keeps only files modified within a window.</summary>
    /// <param name="since">The window.</param>
    /// <returns>This builder.</returns>
    public ImportTurnsRequestBuilder Since(TimeSpan since)
    {
        _since = since;
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public ImportTurnsRequest Build() => new(_files, _since);
}
