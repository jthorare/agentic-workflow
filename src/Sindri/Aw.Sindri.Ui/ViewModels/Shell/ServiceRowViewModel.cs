using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>
/// One scheduled service on the System page (system spec §2): its cadence, a status word, what it does, its last run, and
/// the catalog's Run now and Pause or Resume as controls for this service. A row lives as long as the service is listed,
/// so a control's outcome survives the re-reads that follow every event.
/// </summary>
/// <param name="name">The service's name.</param>
internal sealed class ServiceRowViewModel(string name) : ObservableObject
{
    /// <summary>The service's name.</summary>
    public string Name { get; } = name;

    /// <summary>"every 5s".</summary>
    public string Every { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>One plain sentence for what it does, or empty for a service the app does not know.</summary>
    public string What { get; } = Labels.ServiceWhat(name);

    /// <summary>Scheduled, Running now, Paused or Last run failed.</summary>
    public string Status { get; private set => Set(ref field, value); } = Labels.ServiceScheduled;

    /// <summary>"last run 12:01:30" or "not run yet".</summary>
    public string LastRun { get; private set => Set(ref field, value); } = Labels.ServiceNotRun;

    /// <summary>True while the service is paused.</summary>
    public bool IsPaused { get; private set => Set(ref field, value); }

    /// <summary>True while a manual run's job is in flight.</summary>
    public bool IsRunning { get; private set => Set(ref field, value); }

    /// <summary>True when its latest run failed.</summary>
    public bool HasFailed { get; private set => Set(ref field, value); }

    /// <summary>The job of the manual run in flight, or empty.</summary>
    public string JobId { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Run now for this service, or null before the catalog is read.</summary>
    public ControlViewModel? Run
    {
        get;
        internal set
        {
            Set(ref field, value);
            OnChanged(nameof(RunShown));
        }
    }

    /// <summary>Pause for a running schedule, Resume for a paused one; null before the catalog is read.</summary>
    public ControlViewModel? Toggle
    {
        get;
        internal set
        {
            Set(ref field, value);
            OnChanged(nameof(ToggleShown));
        }
    }

    /// <summary>True when the service allows Run now.</summary>
    public bool RunAllowed
    {
        get;
        internal set
        {
            Set(ref field, value);
            OnChanged(nameof(RunShown));
        }
    } = true;

    /// <summary>True when Run now is offered: the catalog is read and the service allows it.</summary>
    public bool RunShown => Run is not null && RunAllowed;

    /// <summary>True when the service allows the toggle's action (Pause, or Resume while paused).</summary>
    public bool ToggleAllowed
    {
        get;
        internal set
        {
            Set(ref field, value);
            OnChanged(nameof(ToggleShown));
        }
    } = true;

    /// <summary>True when Pause or Resume is offered: the catalog is read and the service allows it.</summary>
    public bool ToggleShown => Toggle is not null && ToggleAllowed;

    /// <summary>True when <see cref="Toggle"/> was made for a paused service (it resumes).</summary>
    internal bool ToggleResumes { get; set; }

    /// <summary>Shows the service as the API serves it.</summary>
    /// <param name="view">The row.</param>
    internal void Show(ServiceRowView view)
    {
        Every = Labels.ServiceEvery(view.Every);
        IsPaused = view.Paused;
        IsRunning = !string.IsNullOrEmpty(view.Job);
        HasFailed = view.LastRun is { } run && string.Equals(run.Outcome, "failed", StringComparison.Ordinal);
        JobId = view.Job ?? string.Empty;
        LastRun = view.LastRun is { } last ? "last run " + StatusText.Time(last.RanAt) : Labels.ServiceNotRun;
        Status = (IsRunning, IsPaused, HasFailed) switch
        {
            (true, _, _) => Labels.ServiceRunning,
            (false, true, _) => Labels.ServicePaused,
            (false, false, true) => Labels.ServiceLastRunFailed,
            (false, false, false) => Labels.ServiceScheduled,
        };
    }
}
