using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Jobs;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Instance;

namespace Aw.Sindri.Ui.ViewModels.Jobs;

/// <summary>
/// The job sheet (API P6): one job in words, with its progress, Cancel while it runs, what it produced under a closed disclosure and the technical
/// fields under Details. It reads the job on open and again on every event while the sheet is open (the shell gates that).
/// </summary>
/// <param name="api">The API.</param>
internal sealed partial class JobSheetViewModel(ISindriApi api) : ObservableObject
{
    private Uri? _url;
    private int _generation;
    private int _copies;
    private CatalogViewModel? _catalog;
    private TimeProvider _clock = TimeProvider.System;
    private JobView? _last;

    /// <summary>The API the sheet reads.</summary>
    internal ISindriApi Api { get; } = api;

    /// <summary>The job's id; empty before the first.</summary>
    public string Id { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Where the read stands.</summary>
    public FramePhase Phase
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(IsShown));
            OnChanged(nameof(IsLoading));
            OnChanged(nameof(IsProblem));
        }
    }

    /// <summary>True while a job is shown.</summary>
    public bool IsShown => Phase == FramePhase.Shown;

    /// <summary>True before the first answer.</summary>
    public bool IsLoading => Phase == FramePhase.Loading;

    /// <summary>True while an unreadable, unknown or refused answer is the whole sheet.</summary>
    public bool IsProblem => Phase is FramePhase.Unreadable or FramePhase.Refused or FramePhase.NotFound;


    /// <summary>The kind in words ("Run service"); the id until the read draws.</summary>
    public string Heading { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The state line: chip, when, who started it.</summary>
    public string StateLine { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the job failed: the sheet shows its summary in a red callout.</summary>
    public bool IsFailedJob { get; private set => Set(ref field, value); }

    /// <summary>How it ended, as served; empty while it runs.</summary>
    public string SummaryText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when there is a summary.</summary>
    public bool HasSummary => SummaryText.Length > 0;

    /// <summary>The Cancel control while the API allows it; else null.</summary>
    public ControlViewModel? Cancel { get; private set => Set(ref field, value); }

    /// <summary>The progress lines, oldest first.</summary>
    public IReadOnlyList<string> Progress
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasProgress));
            OnChanged(nameof(HasNoProgress));
        }
    } = [];

    /// <summary>True when the job has progress to list.</summary>
    public bool HasProgress => Progress.Count > 0;

    /// <summary>True when it has said nothing yet.</summary>
    public bool HasNoProgress => IsShown && !HasProgress;

    /// <summary>What the job produced, as indented JSON; empty when it produced nothing.</summary>
    public string ResultText
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasResult));
        }
    } = string.Empty;

    /// <summary>True when there is a result to disclose.</summary>
    public bool HasResult => ResultText.Length > 0;

    /// <summary>The technical fields: the id, the kind as served, the key, what it works on, the code and who cancelled it.</summary>
    public IReadOnlyList<JobFieldViewModel> Fields { get; private set => Set(ref field, value); } = [];

    /// <summary>The cancel's terminal form, to copy; empty when the job cannot be cancelled.</summary>
    public string Cli
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasCli));
        }
    } = string.Empty;

    /// <summary>True when the terminal form is shown.</summary>
    public bool HasCli => Cli.Length > 0;


    /// <summary>A refusal's or an unreadable answer's sentence; empty otherwise.</summary>
    public string ProblemText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The code of a refused or unknown answer, for Details; empty otherwise.</summary>
    public string ProblemCode
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasProblemCode));
        }
    } = string.Empty;

    /// <summary>True when there is a code to disclose.</summary>
    public bool HasProblemCode => ProblemCode.Length > 0;

    /// <summary>The stale note: the last read failed and the fields are the last read's.</summary>
    public string Note { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Gives the sheet the catalog (the Cancel row, the clipboard) and the clock (Copy's "Copied", the ages).</summary>
    /// <param name="catalog">The command catalog.</param>
    /// <param name="clock">The clock.</param>
    internal void Wire(CatalogViewModel catalog, TimeProvider clock)
    {
        (_catalog, _clock) = (catalog, clock);
        catalog.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(CatalogViewModel.Controls), StringComparison.Ordinal) && _last is { } job)
            {
                Cancel = CancelFor(job);   // a catalog read after the sheet drew gives a running job its Cancel
            }
        };
    }

    /// <summary>Names the job now, before any read (a deep link, even before the app is connected).</summary>
    /// <param name="id">The job's id.</param>
    internal void Show(string id)
    {
        if (string.Equals(id, Id, StringComparison.Ordinal))
        {
            return;
        }

        _generation++;
        Clear();
        Id = id;
        Phase = FramePhase.Loading;
    }

    /// <summary>Reads the job behind an id and shows it.</summary>
    /// <param name="url">The base URL.</param>
    /// <param name="id">The job's id.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that ends when the read has been drawn.</returns>
    internal Task Open(Uri url, string id, CancellationToken cancellationToken)
    {
        Show(id);
        _url = url;
        return Read(cancellationToken);
    }

    /// <summary>Reads the open job again (on every event while the sheet is open).</summary>
    /// <param name="url">The base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that ends when the read has been drawn.</returns>
    internal Task Refresh(Uri url, CancellationToken cancellationToken)
    {
        _url = url;
        return Id.Length == 0 ? Task.CompletedTask : Read(cancellationToken);
    }

    private async Task Read(CancellationToken cancellationToken)
    {
        if (_url is not { } url || Id.Length == 0)
        {
            return;
        }

        var generation = ++_generation;
        var answer = await Api.Job(url, Id, cancellationToken).ConfigureAwait(true);
        if (generation != _generation)
        {
            return;   // a newer read, or another job, took over
        }

        if (answer.IsSuccess)
        {
            Draw(answer.Value);
        }
        else
        {
            Failed(answer.Error);
        }
    }

    private void Draw(JobView job)
    {
        var running = string.Equals(job.State, "running", StringComparison.Ordinal);
        var (glyph, word) = Labels.JobChip(job.State);
        var age = _clock.GetUtcNow() - (running ? job.StartedAt : job.EndedAt ?? job.StartedAt);
        Heading = Labels.JobKind(job.Kind);
        StateLine = string.Join(" · ", glyph + " " + word, Labels.JobWhen(running, age), "by " + Labels.JobStartedBy(job.StartedBy));
        SummaryText = job.Summary ?? string.Empty;
        IsFailedJob = string.Equals(job.State, "failed", StringComparison.Ordinal);
        OnChanged(nameof(HasSummary));
        _last = job;
        Cancel = job.Actions.Contains("CancelJob", StringComparer.Ordinal) ? Cancel ?? CancelFor(job) : null;
        Progress = [.. job.Progress.Select(p => p.Line)];
        ResultText = JobFields.Result(job.Result);
        Cli = job.Cli ?? string.Empty;
        Fields = JobFields.Technical(job);
        (ProblemText, Note) = (string.Empty, string.Empty);
        Phase = FramePhase.Shown;
        OnChanged(nameof(HasNoProgress));
    }

    private ControlViewModel? CancelFor(JobView job) =>
        job.Actions.Contains("CancelJob", StringComparer.Ordinal)
            ? _catalog?.On("CancelJob", Heading, ct => _url is { } url ? Api.CancelJob(url, job.Id, ct) : Task.FromResult<Result<CommandAcceptedView>>(SindriUiErrors.Unreachable.With("Sindri isn't reachable yet")))
            : null;

    private void Failed(Error error)
    {
        if (IsShown)
        {
            Note = Labels.JobStale(error);
            return;
        }

        var unreadable = string.Equals(error.Code, SindriUiErrors.BadAnswer.Code, StringComparison.Ordinal);
        var unknown = !unreadable && error.Kind is ErrorKind.NotFound or ErrorKind.Invalid;   // an unknown id and a garbage one are the same to the reader
        ProblemText = unknown ? Labels.JobUnknown : unreadable ? Labels.JobsUnreadable : Labels.InstanceRefused(error);
        ProblemCode = unknown ? error.Code : string.Empty;
        Phase = unknown ? FramePhase.NotFound : unreadable ? FramePhase.Unreadable : FramePhase.Refused;
    }

    private void Clear()
    {
        (Heading, StateLine, SummaryText, ProblemText, Note, ResultText, Cli) = (string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
        (Progress, Fields, Cancel, _last) = ([], [], null, null);
        (ProblemCode, CopyLabel) = (string.Empty, Labels.Copy);
        _copies++;   // a pending "Copied" reset belongs to the job that was copied
        OnChanged(nameof(HasSummary));
    }
}
