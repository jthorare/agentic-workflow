using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>
/// The top bar's health pill (UI spec §3.2, §4.1, §11.3): a dot, a glyph and the state's word, then how fresh the views
/// are. It maps the shell's state to words; the connection (<see cref="HealthViewModel"/>) decides the state.
/// </summary>
internal sealed class HealthPill : ObservableObject
{
    // The views' own problems (stalled, replaying): the shell's banner says them once, and the pill's word is the state.
    private static readonly HashSet<string> _viewsCodes = new(["SND-VIEW-001", "SND-API-010"], StringComparer.Ordinal);

    /// <summary>The state's glyph (§11.3): ✓ running, ⌛ stale, ✕ not running or unreadable; none while connecting.</summary>
    public StateGlyph Glyph
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasGlyph));
        }
    }

    /// <summary>The state's word. After three failed refreshes it stays "stale"; the shell's banner says Sindri isn't responding.</summary>
    public string Word { get; private set => Set(ref field, value); } = Labels.Connecting;

    /// <summary>"updated Ns ago" once connected, "stale" when unreadable, or empty.</summary>
    public string Age
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasAge));
        }
    } = string.Empty;

    /// <summary>Any problem the status's health names, in words as served (§3.2: "index failed"); empty otherwise.</summary>
    public string Problems
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasProblems));
        }
    } = string.Empty;

    /// <summary>True when the pill names problems.</summary>
    public bool HasProblems => Problems.Length > 0;

    /// <summary>True when the state has a glyph; while connecting it has none, and the pill drops the glyph's slot (S1).</summary>
    public bool HasGlyph => Glyph != StateGlyph.None;

    /// <summary>True when the pill shows an age after its word.</summary>
    public bool HasAge => Age.Length > 0;

    /// <summary>The pill in words (its accessible name): the word, and the age after a separator.</summary>
    public string Text { get; private set => Set(ref field, value); } = Labels.Connecting;

    /// <summary>True when the dot shows a healthy control plane.</summary>
    public bool IsHealthy { get; private set => Set(ref field, value); }

    /// <summary>True when the dot shows stale views.</summary>
    public bool IsStale { get; private set => Set(ref field, value); }

    /// <summary>True when the dot shows nothing to follow: not running, unreadable, or a version mismatch.</summary>
    public bool IsDown { get; private set => Set(ref field, value); }

    /// <summary>Stale after three failed refreshes (§4.1): the shell shows "Sindri not responding: run sindri doctor" in a banner.</summary>
    public bool IsNotResponding { get; private set => Set(ref field, value); }

    /// <summary>The views shown are stale (R3, §4.1): stale, or unreadable with the last data kept.</summary>
    public bool IsDataStale { get; private set => Set(ref field, value); }

    /// <summary>Stale: the Refresh button beside the pill (§4.1).</summary>
    public bool IsRefreshShown => IsStale;

    /// <summary>Words the pill for a state.</summary>
    /// <param name="state">The shell's state.</param>
    /// <param name="age">How long since the views were last heard from.</param>
    /// <param name="notResponding">True after three failed refreshes while stale.</param>
    /// <param name="health">The status's health (E-14), or null before API P2.</param>
    internal void Show(ShellState state, TimeSpan age, bool notResponding, HealthView? health = null)
    {
        (Glyph, Word, Age) = state switch
        {
            ShellState.Normal when health is not null => (Glyphs(health.Word), Labels.HealthWord(health.Word), Labels.UpdatedAgo(age)),
            ShellState.Normal => (StateGlyph.Ok, Labels.Running, Labels.UpdatedAgo(age)),
            ShellState.Stale => (StateGlyph.Stale, Labels.Stale, Labels.UpdatedAgo(age)),
            ShellState.NotRunning => (StateGlyph.Down, Labels.NotRunning, string.Empty),
            ShellState.Unreadable => (StateGlyph.Down, Labels.Unreadable, Labels.Stale),
            ShellState.VersionMismatch => (StateGlyph.Down, Labels.VersionMismatch, string.Empty),
            ShellState.Connecting => (StateGlyph.None, Labels.Connecting, string.Empty),
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, message: null),
        };
        Problems = state == ShellState.Normal && health is not null ? string.Join(" · ", health.Problems.Where(p => !_viewsCodes.Contains(p.Code)).Select(p => p.Clause ?? p.Text)) : string.Empty;
        Text = string.Join(" · ", ((string[])[Word, Age, Problems]).Where(t => t.Length > 0));
        IsHealthy = state == ShellState.Normal && (health is null || string.Equals(health.Word, "healthy", StringComparison.Ordinal));
        var stale = state == ShellState.Stale;
        if (stale != IsStale)
        {
            IsStale = stale;
            OnChanged(nameof(IsRefreshShown));
        }

        IsNotResponding = stale && notResponding;
        IsDataStale = state is ShellState.Stale or ShellState.Unreadable;
        IsDown = state is ShellState.NotRunning or ShellState.Unreadable or ShellState.VersionMismatch;
    }

    // §11.3: healthy ✓, rebuilding ⌛ (the views are catching up), degraded △, stalled ✕; always beside the word.
    private static StateGlyph Glyphs(string word) => word switch
    {
        "healthy" => StateGlyph.Ok,
        "rebuilding" => StateGlyph.Stale,
        "degraded" => StateGlyph.Warning,
        _ => StateGlyph.Down,
    };
}
