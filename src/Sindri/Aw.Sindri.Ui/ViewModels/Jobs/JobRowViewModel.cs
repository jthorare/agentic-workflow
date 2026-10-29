using Aw.Sindri.Ui.Api.Jobs;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Jobs;

/// <summary>
/// One job on the Jobs page. It is kept by its id across reads and updated in place, so a control with focus keeps it. It maps served fields to words and
/// decides nothing: the Cancel control is the catalog's CancelJob row, shown while the job runs.
/// </summary>
internal sealed class JobRowViewModel : ObservableObject
{
    private readonly TimeProvider _clock;
    private readonly Func<string, string, ControlViewModel?> _cancel;
    private readonly Func<string, CancellationToken, Task>? _resume;

    internal JobRowViewModel(JobRowView row, Func<string, string, ControlViewModel?> cancel, TimeProvider clock, Func<string, CancellationToken, Task>? resume = null)
    {
        (Row, _cancel, _clock, _resume) = (row, cancel, clock, resume);
        Link = new DeepLink(DeepLinkKind.Job, row.Id);
        Derive();
    }

    /// <summary>The job as last served.</summary>
    internal JobRowView Row { get; private set; }

    /// <summary>The job's id.</summary>
    public string Id => Row.Id;

    /// <summary>The deep link that opens its sheet.</summary>
    public DeepLink Link { get; }

    /// <summary>The kind in words, and what it works on: "Run service · heartbeat".</summary>
    public string TitleText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The job's key, shown in grey beside the kind.</summary>
    public string KeyText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"3 rounds".</summary>
    public string RoundsText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The chip: glyph and word.</summary>
    public string ChipText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"Started 3m ago" while it runs, "Ended 3m ago" after.</summary>
    public string WhenText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"by Sindri".</summary>
    public string ByText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>How it ended, as served; empty while it runs.</summary>
    public string SummaryText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The error code of a failed job, muted after the summary; empty otherwise.</summary>
    public string CodeText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The second line of the row: what the job's model calls came to, or why it waits; empty when there is none.</summary>
    public string ModelText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the row has a model line.</summary>
    public bool HasModelText => ModelText.Length > 0;

    /// <summary>True while the job is parked on 'model-limit': the row shows Resume.</summary>
    public bool HasResume { get; private set => Set(ref field, value); }

    /// <summary>The Resume button's words.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string ResumeText => Labels.JobResume;
#pragma warning restore CA1822, MA0041

    /// <summary>The Resume button's accessible name: "Resume Build the context map".</summary>
    public string ResumeName => Labels.JobResumeName(TitleText);

    /// <summary>Sends the resume of this job and reads the list again; a job that is no longer waiting is refused by Sindri and the read shows it as it is now.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that ends when the list was read again.</returns>
    internal Task Resume(CancellationToken cancellationToken)
    {
        _ = (_resume, cancellationToken);
        throw new NotSupportedException();
    }

    /// <summary>The row's accessible name.</summary>
    public string Name { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The Cancel control while the job runs and the catalog has the row; else null.</summary>
    public ControlViewModel? Cancel { get; private set => Set(ref field, value); }

    /// <summary>True while the job runs.</summary>
    public bool IsRunning => string.Equals(Row.State, "running", StringComparison.Ordinal);

    /// <summary>True when the row has a summary.</summary>
    public bool HasSummary => SummaryText.Length > 0;

    /// <summary>True when the row has a summary and room for it.</summary>
    public bool HasWideSummary => HasSummary && IsWide;

    /// <summary>True when the row has an error code.</summary>
    public bool HasCode => CodeText.Length > 0;

    /// <summary>True under 760 px: the row keeps only its job, status and rounds.</summary>
    public bool IsCompact { get; private set => Set(ref field, value); }

    /// <summary>True when the row keeps every column.</summary>
    public bool IsWide => !IsCompact;

    /// <summary>True when the row has a Cancel control.</summary>
    public bool HasCancel => Cancel is not null;

    /// <summary>Narrows or widens the row (the page crossed 760 px).</summary>
    /// <param name="compact">True under 760 px.</param>
    internal void SetCompact(bool compact)
    {
        IsCompact = compact;
        OnChanged(nameof(IsWide));
        OnChanged(nameof(HasWideSummary));
    }

    /// <summary>Takes a newer serving of the same job and redraws only what changed.</summary>
    /// <param name="row">The row as served now.</param>
    internal void Update(JobRowView row)
    {
        Row = row;
        Derive();
    }

    /// <summary>Re-words the age (time passes without a read).</summary>
    internal void Age() => WhenText = When();

    /// <summary>Gives a running job its Cancel control now that the catalog is read.</summary>
    internal void Recatalog() => Derive();

    private void Derive()
    {
        var (glyph, word) = Labels.JobChip(Row.State);
        ChipText = glyph + " " + word;
        TitleText = Labels.JobKind(Row.Kind);
        (KeyText, RoundsText) = (Row.Key, Labels.JobRounds(Row.Rounds));
        WhenText = When();
        ByText = "by " + Labels.JobStartedBy(Row.StartedBy);
        (SummaryText, CodeText) = (Row.Summary ?? string.Empty, Row.Code ?? string.Empty);
        Cancel = IsRunning ? Cancel ?? _cancel(Row.Id, TitleText) : null;
        ModelText = Row.Models is null && Row.WaitingOn is null ? string.Empty : ModelLines.Row(Row) ?? string.Empty;
        HasResume = string.Equals(Row.WaitingOn, ModelLines.ModelLimit, StringComparison.Ordinal);
        Name = Labels.JobRowName(TitleText, word.ToLowerInvariant(), WhenText, Labels.JobStartedBy(Row.StartedBy), SummaryText);
        foreach (var name in (string[])[nameof(IsRunning), nameof(HasSummary), nameof(HasWideSummary), nameof(HasCode), nameof(HasCancel), nameof(HasModelText), nameof(ResumeName)])
        {
            OnChanged(name);
        }
    }

    private string When() => IsRunning
        ? Labels.JobWhen(running: true, _clock.GetUtcNow() - Row.StartedAt)
        : Labels.JobWhen(running: false, _clock.GetUtcNow() - (Row.EndedAt ?? Row.StartedAt));
}
