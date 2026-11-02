using Aw.Judge.Features.ImportLabels;

namespace Aw.Judge.Testing;

/// <summary>Builds an ImportLabelsRequest with deterministic defaults: every question, the last 14 days.</summary>
public sealed class ImportLabelsRequestBuilder
{
    private TimeSpan _since = TimeSpan.FromDays(14);
    private string? _question;

    /// <summary>Sets how far back to look.</summary>
    /// <param name="since">The window.</param>
    /// <returns>This builder.</returns>
    public ImportLabelsRequestBuilder Since(TimeSpan since)
    {
        _since = since;
        return this;
    }

    /// <summary>Limits the import to one question.</summary>
    /// <param name="question">The question.</param>
    /// <returns>This builder.</returns>
    public ImportLabelsRequestBuilder Question(string question)
    {
        _question = question;
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public ImportLabelsRequest Build() => new(_since, _question);
}
