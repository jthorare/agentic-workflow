using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Setup;

namespace Aw.Sindri.Ui.ViewModels.Settings;

/// <summary>
/// Settings (UI spec §3.3 ⌘,, §11.4, §12.3, §14.2): the in-app Reduce motion switch and the text size, kept in the local
/// preferences. Reduced motion is the OS setting or the switch, re-read when the OS setting changes. Each change goes to
/// the theme through the apply callback (no Avalonia types here). Built on the UI thread: an OS change raised elsewhere is
/// posted back to it.
/// </summary>
internal sealed class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly Preferences _preferences;
    private readonly IMotionPreference _motion;
    private readonly Action<AppearanceSettings> _apply;
    private readonly UiContext _ui = new();
    private bool _disposed;
    private bool _loading;

    /// <summary>Settings over the preferences, the OS setting and the theme.</summary>
    /// <param name="preferences">The app's local preferences, shared with the shell.</param>
    /// <param name="motion">The OS reduce-motion setting.</param>
    /// <param name="apply">Applies the appearance to the theme (on the UI thread).</param>
    /// <param name="setup">The setup checklist, Settings' first section; null where the page has none (a host that shows only the local preferences).</param>
    internal SettingsViewModel(Preferences preferences, IMotionPreference motion, Action<AppearanceSettings> apply, SetupViewModel? setup = null)
    {
        Setup = setup;
        _preferences = preferences;
        _motion = motion;
        _apply = apply;
        SystemReducesMotion = motion.ReduceMotion;
        motion.Changed += OnMotionChanged;
    }

    /// <summary>The page title.</summary>
    public string Title { get; } = Labels.Settings;

    /// <summary>The setup checklist, the page's first section; null when this host has none.</summary>
    public SetupViewModel? Setup { get; }

    /// <summary>True when the page has the setup checklist.</summary>
    public bool HasSetup => Setup is not null;

    /// <summary>The heading over the app's own preferences, below the checklist.</summary>
    public string ThisAppLabel { get; } = Labels.ThisApp;

    /// <summary>The switch's label.</summary>
    public string ReduceMotionLabel { get; } = Labels.ReduceMotion;

    /// <summary>The note shown while the OS reduces motion (the switch can't turn that off).</summary>
    public string SystemReducesMotionNote { get; } = Labels.SystemReducesMotion;

    /// <summary>The text size slider's label.</summary>
    public string TextSizeLabel { get; } = Labels.TextSize;

    /// <summary>The smallest text size.</summary>
    public double MinTextScale { get; } = UiPreferences.MinTextScale;

    /// <summary>The largest text size.</summary>
    public double MaxTextScale { get; } = UiPreferences.MaxTextScale;

    /// <summary>The smallest text size in words ("90%"), the slider's left end.</summary>
    public string MinTextScaleText => throw new NotSupportedException();

    /// <summary>The largest text size in words ("130%"), the slider's right end.</summary>
    public string MaxTextScaleText => throw new NotSupportedException();

    /// <summary>The in-app Reduce motion switch.</summary>
    public bool ReduceMotion
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            OnChanged(nameof(ReduceMotion));
            OnChanged(nameof(IsMotionReduced));
            Changed();
        }
    }

    /// <summary>True while the OS asks for reduced motion.</summary>
    public bool SystemReducesMotion
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(IsMotionReduced));
        }
    }

    /// <summary>True when motion is reduced: the OS setting or the switch (UI spec §11.4).</summary>
    public bool IsMotionReduced => ReduceMotion || SystemReducesMotion;

    /// <summary>The text size, kept within 0.9–1.3 by the preferences' clamp.</summary>
    public double TextScale
    {
        get;
        set
        {
            var clamped = Math.Round((UiPreferences.Defaults with { TextScale = value }).Clamp().TextScale, 2, MidpointRounding.AwayFromZero);
            if (field.Equals(clamped))
            {
                OnChanged(nameof(TextScale)); // a slider that asked for more than the range shows the clamped value
                return;
            }

            field = clamped;
            OnChanged(nameof(TextScale));
            OnChanged(nameof(TextScaleText));
            Changed();
        }
    } = UiPreferences.Defaults.TextScale;

    /// <summary>The text size in words ("110%").</summary>
    public string TextScaleText => Labels.Percent(TextScale);

    /// <summary>Reads the stored preferences and the OS setting, and applies them (the shell's start-up calls it once).</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when they're applied.</returns>
    internal async Task Load(CancellationToken cancellationToken)
    {
        var stored = await _preferences.Load(cancellationToken).ConfigureAwait(true);
        _loading = true; // what was read isn't written back
        try
        {
            ReduceMotion = stored.ReduceMotion;
            TextScale = stored.TextScale;
            SystemReducesMotion = _motion.ReduceMotion;
        }
        finally
        {
            _loading = false;
        }

        Apply();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
        _motion.Changed -= OnMotionChanged;
    }

    // A change the user made: shown at once and kept.
    private void Changed()
    {
        if (_loading)
        {
            return;
        }

        Apply();
        Save();
    }

    private void Apply() => _apply(new AppearanceSettings(TextScale, IsMotionReduced));

    // Writes the two values over whatever else the file holds by then, through the app's one write queue (shared with the
    // shell). A refused write leaves the setting applied for this session; the next change tries again.
    private void Save()
    {
        var (scale, reduce) = (TextScale, ReduceMotion);
        _ = _preferences.Update(p => p with { TextScale = scale, ReduceMotion = reduce });
    }

    private void OnMotionChanged(object? sender, EventArgs e) => _ui.PostOrInline(Reread);

    private void Reread()
    {
        if (_disposed)
        {
            return;
        }

        var reduced = IsMotionReduced;
        SystemReducesMotion = _motion.ReduceMotion;
        if (IsMotionReduced != reduced)
        {
            Apply();
        }
    }
}
