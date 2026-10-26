namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The Health page's status card (<see cref="HealthViewModel"/>, continued): the lease, the API, the profile, the services.</summary>
internal sealed partial class HealthViewModel
{
    /// <summary>
    /// The live region's text (§4.1 "sending"): "Sending…" once a refresh has been in flight for 300 ms, otherwise its
    /// resting text, "Refresh". Never empty (Avalonia #22312: a live element's name set to "" crashes on macOS), and the
    /// resting text is never spoken: it is set only once <see cref="IsSending"/>, the region's live gate, is off.
    /// </summary>
    public string SendingAnnouncement { get; private set => Set(ref field, value); } = Labels.Refresh;

    /// <summary>One sentence for how Sindri is ("Everything is running."); the System page leads with it and Home's strip repeats it.</summary>
    public string Sentence { get; private set => Set(ref field, value); } = Labels.Connecting;

    // One sentence for how Sindri is, by the shell's state; it lives here because Labels doesn't depend on the Shell namespace.
    private readonly record struct SentenceInputs(ShellState State, bool HasHealth, bool Healthy, string Problems, bool ProfileFix, string? Served, string Look);

    private static string SentenceFor(SentenceInputs h) => h.State switch
    {
        ShellState.Normal => NormalSentence(h),
        ShellState.Stale => Labels.StoppedAnswering,
        ShellState.NotRunning => Labels.SindriNotRunning,
        ShellState.Unreadable => Labels.CantReadAnswer,
        ShellState.VersionMismatch => Labels.DifferentVersions,
        ShellState.Connecting => Labels.Connecting,
        _ => throw new ArgumentOutOfRangeException(nameof(h), h.State, message: null),
    };

    private static string NormalSentence(SentenceInputs h)
    {
        if (!h.HasHealth)
        {
            return Labels.CheckingHealth;
        }

        if (h.Healthy)
        {
            return h.ProfileFix ? Labels.ProfileNeedsApproval
                : h.Look.Length > 0 ? Labels.NeedsALook(h.Look)   // the server says healthy, a card says otherwise
                : Labels.AllRunning;
        }

        if (!string.IsNullOrWhiteSpace(h.Served))
        {
            return h.Served;   // Sindri's own sentence: "Sindri is degraded: the code index for demo is behind main."
        }

        return h.Problems.Length > 0 ? Labels.NeedsALook(h.Problems) : Labels.RunningNeedsALook;
    }

    /// <summary>Home's health strip's accessible name (R3): the health line, then "stale" while the views are stale.</summary>
    public string StripName { get; private set => Set(ref field, value); } = Labels.Connecting;

    /// <summary>Not running: the reason's detail, on its own mono line under it (the endpoint record's path, shortened, the address, or the record against the answer); or empty.</summary>
    public string ReasonDetail { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The command that fixes "not running".</summary>
    public string Fix { get; } = Labels.StartFix;

    /// <summary>Running: the lease line.</summary>
    public string Lease { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Where the API listens.</summary>
    public string Api { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The profile's state in words.</summary>
    public string ProfileState { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The profile's hash and file.</summary>
    public string ProfileDetail { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The command that fixes the profile's state, or empty.</summary>
    public string ProfileFix
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasProfileFix));
        }
    } = string.Empty;

    /// <summary>True when the profile needs a command.</summary>
    public bool HasProfileFix => ProfileFix.Length > 0;

    /// <summary>The scheduled services.</summary>
    public IReadOnlyList<ServiceRow> Services
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasServices));
        }
    } = [];

    /// <summary>True when any service is scheduled.</summary>
    public bool HasServices => Services.Count > 0;
}
