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

    internal JobRowViewModel(JobRowView row, Func<string, string, ControlViewModel?> cancel, TimeProvider clock)
    {
        (Row, _cancel, _clock) = (row, cancel, clock);
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

    /// <summary>The row's accessible name.</summary>
    public string Name { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The Cancel control while the job runs and the catalog has the row; else null.</summary>
    public ControlViewModel? Cancel { get; private set => Set(ref field, value); }

    /// <summary>True while the job runs.</summary>
    public bool IsRunning => string.Equals(Row.State, "running", StringComparison.Ordinal);

    /// <summary>True when the row has a summary.</summary>
    public bool HasSummary => SummaryText.Length > 0;

    /// <summary>True when the row has an error code.</summary>
    public bool HasCode => CodeText.Length > 0;

    /// <summary>True when the row has a Cancel control.</summary>
    public bool HasCancel => Cancel is not null;

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
        TitleText = TitleOf(Row);
        WhenText = When();
        ByText = "by " + Labels.JobStartedBy(Row.StartedBy);
        (SummaryText, CodeText) = (Row.Summary ?? string.Empty, Row.Code ?? string.Empty);
        Cancel = IsRunning ? Cancel ?? _cancel(Row.Id, TitleText) : null;
        Name = Labels.JobRowName(TitleText, word.ToLowerInvariant(), WhenText, Labels.JobStartedBy(Row.StartedBy), SummaryText);
        foreach (var name in (string[])[nameof(IsRunning), nameof(HasSummary), nameof(HasCode), nameof(HasCancel)])
        {
            OnChanged(name);
        }
    }

    private string When() => IsRunning
        ? Labels.JobWhen(running: true, _clock.GetUtcNow() - Row.StartedAt)
        : Labels.JobWhen(running: false, _clock.GetUtcNow() - (Row.EndedAt ?? Row.StartedAt));

    // The kind as the catalog words an action ("Run service"), and what the job works on when it says: the service, the item or the repo.
    private static string TitleOf(JobRowView row)
    {
        var kind = Labels.Action(row.Kind);
        var on = row.Service ?? row.Item ?? row.Repo;
        return on is { Length: > 0 } ? kind + " · " + on : kind;
    }
}
