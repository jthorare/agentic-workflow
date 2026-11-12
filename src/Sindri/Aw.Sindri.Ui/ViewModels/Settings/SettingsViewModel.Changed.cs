using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Settings;

/// <summary>Settings' changed marks and Resets (#3362): a setting that differs from its default says so and offers a way back, as VS Code's modified settings do.</summary>
internal sealed partial class SettingsViewModel
{
    private const double SameScale = 0.001;

    /// <summary>True when the text size is not the default 100%.</summary>
    public bool TextScaleChanged => Math.Abs(TextScale - UiPreferences.Defaults.TextScale) > SameScale;

    /// <summary>True when the in-app Reduce motion switch is on (the default is off).</summary>
    public bool ReduceMotionChanged => ReduceMotion;

    /// <summary>How many settings differ from their defaults.</summary>
    public int ChangedCount => (TextScaleChanged ? 1 : 0) + (ReduceMotionChanged ? 1 : 0);

    /// <summary>The mark shown beside a changed setting's name.</summary>
    public string ChangedMark { get; } = Labels.SettingChanged;

    /// <summary>The Reset button's label.</summary>
    public string ResetLabel { get; } = Labels.SettingReset;

    /// <summary>True when the "This app" section goes above the setup checklist: the page has no checklist, or setup is finished.</summary>
    public bool ThisAppFirst => Setup is null || Setup.IsFinished;

    /// <summary>Puts the text size back to 100%.</summary>
    internal void ResetTextScale() => TextScale = UiPreferences.Defaults.TextScale;

    /// <summary>Turns the in-app Reduce motion switch off.</summary>
    internal void ResetReduceMotion() => ReduceMotion = false;

    // The checklist finishing moves "This app" above it (#3362).
    private void WatchSetup() =>
        Setup?.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(Setup.IsFinished), StringComparison.Ordinal))
            {
                OnChanged(nameof(ThisAppFirst));
            }
        };

    private void NotifyChangedMarks()
    {
        OnChanged(nameof(TextScaleChanged));
        OnChanged(nameof(ReduceMotionChanged));
        OnChanged(nameof(ChangedCount));
    }
}
