using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Sources;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>
/// Health's Context index card (#1597): one line per context source from GET /sindri/index/status, so a source that isn't pulling and holds
/// nothing can't hide behind another that is; then the concepts: how many, the share citing two or more sources, and what isn't labeled yet.
/// Followed like the other views: read on connect and after every event, never folded.
/// </summary>
/// <param name="api">The API.</param>
/// <param name="clock">For the age of the last commit and the minutes to a retry.</param>
internal sealed class ContextIndexCardViewModel(ISindriApi api, TimeProvider clock) : ObservableObject
{
    /// <summary>The card's heading.</summary>
    public string Heading { get; } = Labels.ContextIndexCard;

    /// <summary>The lines, one per source and then the concepts'; empty when no source is set up or nothing has been read.</summary>
    public IReadOnlyList<ContextIndexLine> Lines { get; private set => Set(ref field, value); } = [];

    /// <summary>What shows instead of lines: the reading, none-set-up or unreadable words; empty while lines show.</summary>
    public string Message { get; private set => Set(ref field, value); } = Labels.ReadingContextIndex;

    /// <summary>True when there is a message to show.</summary>
    public bool HasMessage => Message.Length > 0;

    /// <summary>The state in a word at the heading's right: "ok" or "needs a look"; empty with no lines.</summary>
    public string Word { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Reads the status and shows it.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal async Task Refresh(Uri url, CancellationToken cancellationToken)
    {
        var read = await api.IndexStatus(url, cancellationToken).ConfigureAwait(true);
        if (read.IsError)
        {
            Lines = [];
            Word = string.Empty;
            Show(Labels.ContextIndexUnreadable);
            return;
        }

        var (sources, concepts) = (read.Value.Sources, read.Value.Concepts);
        Lines = sources.Count == 0 ? [] : [.. sources.Select(Line).Concat(ConceptLines(concepts)).Select(t => new ContextIndexLine(t))];
        var fine = sources.All(static s => !NeedsLook(s)) && concepts?.Code is null;
        Word = sources.Count == 0 ? string.Empty : fine ? Labels.CodeIndexOk : Labels.CodeIndexNeedsLook;
        Show(sources.Count == 0 ? Labels.ContextIndexNone : string.Empty);
    }

    private void Show(string message)
    {
        Message = message;
        OnChanged(nameof(HasMessage));
    }

    private static bool Is(string? value, string word) => string.Equals(value, word, StringComparison.Ordinal);

    // A source that isn't pulling and holds nothing is the case this card exists for; a running one with nothing has only just started.
    private static bool NeedsLook(IndexSourceView source) => source.Pull switch
    {
        null => !source.Usable && !string.IsNullOrEmpty(source.LastCode),
        "failed" or "backoff" => true,
        "idle" => source.Records == 0 && source.LastCommit is null,
        _ => false,
    };

    // A failed read carries zero counts, so its code is checked first; queued groups and records in no concept are normal work, not a look.
    private static IEnumerable<string> ConceptLines(IndexConceptsView? concepts)
    {
        if (concepts is null)
        {
            yield break;
        }

        if (concepts.Code is { } code)
        {
            yield return Labels.ConceptsUnreadable(code);
            yield break;
        }

        if (!concepts.Enabled)
        {
            yield return Labels.ConceptsOff;
            yield break;
        }

        yield return Labels.ConceptsCount(concepts.Concepts, concepts.MultiSource);
        if (concepts.Pending is { } pending)
        {
            yield return Labels.ConceptsNotEmbedded(pending);
        }

        yield return Labels.ConceptsQueued(concepts.Queued);
        if (concepts.Embedded is { } embedded)
        {
            yield return Labels.ConceptsUncovered(Math.Max(0, embedded - concepts.Covered), concepts.Coverage);
        }
    }

    private string Line(IndexSourceView source)
    {
        var line = source.Source + ": " + State(source);
        if (source.Records is { } records)
        {
            line += " · " + Labels.ContextRecords(records);
        }

        if (source.LastCommit is { } commit)
        {
            return line + " · " + Labels.ContextLastCommit(clock.GetUtcNow() - commit);
        }

        return source.Records is null ? line : line + " · " + Labels.ContextNothingCommitted;
    }

    private string State(IndexSourceView source) => source.Pull switch
    {
        null => source.Phase ?? Labels.ContextSourceNotStarted,
        "running" => Labels.ContextSourcePulling,
        "idle" => Is(source.LastCode, Labels.ContextCapCode) ? Labels.ContextSourceAtCap : Labels.ContextSourceIdle,
        "backoff" => Labels.ContextSourceRetrying(MinutesToRetry(source.NextTry), source.LastError),
        "failed" => Labels.ContextSourceFailed(source.LastError, source.LastErrorText),
        var other => other,
    };

    private int MinutesToRetry(DateTimeOffset? nextTry) => nextTry is { } at ? Math.Max(1, (int)(at - clock.GetUtcNow()).TotalMinutes) : 1;
}
