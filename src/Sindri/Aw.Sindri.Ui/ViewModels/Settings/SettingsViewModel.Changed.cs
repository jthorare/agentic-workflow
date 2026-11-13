using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Setup;

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

    /// <summary>True when the "This app" section goes above the setup checklist: the page has no checklist, or setup has answered and is finished. Before the first answer the order holds.</summary>
    public bool ThisAppFirst => Setup is null || (Setup.HasAnswered && Setup.IsFinished);

    /// <summary>The Text size Reset button's name.</summary>
    public string ResetTextSizeName => Labels.SettingResetName(TextSizeLabel);

    /// <summary>The Reduce motion Reset button's name.</summary>
    public string ResetMotionName => Labels.SettingResetName(ReduceMotionLabel);

    /// <summary>The Text size slider's accessible name: "Text size, changed" while it is not the default.</summary>
    public string TextSizeName => TextScaleChanged ? Labels.SettingChangedName(TextSizeLabel) : TextSizeLabel;

    /// <summary>The Reduce motion switch's accessible name: "Reduce motion, changed" while it is on.</summary>
    public string ReduceMotionName => ReduceMotionChanged ? Labels.SettingChangedName(ReduceMotionLabel) : ReduceMotionLabel;

    /// <summary>Puts the text size back to 100%.</summary>
    internal void ResetTextScale() => TextScale = UiPreferences.Defaults.TextScale;

    /// <summary>Turns the in-app Reduce motion switch off.</summary>
    internal void ResetReduceMotion() => ReduceMotion = false;

    // The checklist finishing moves "This app" above it (#3362).
    private void WatchSetup() =>
        Setup?.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SetupViewModel.IsFinished) or nameof(SetupViewModel.HasAnswered))
            {
                OnChanged(nameof(ThisAppFirst));
            }
        };

    private void NotifyChangedMarks()
    {
        OnChanged(nameof(TextScaleChanged));
        OnChanged(nameof(ReduceMotionChanged));
        OnChanged(nameof(ChangedCount));
        OnChanged(nameof(TextSizeName));
        OnChanged(nameof(ReduceMotionName));
    }

    /// <summary>Makes a palette Settings entry set the filter: the shell asks for the words and this page takes them.</summary>
    /// <param name="subscribe">Subscribes a handler to the shell's setting-filter request.</param>
    internal void FollowPalette(Action<EventHandler<ValueEventArgs<string>>> subscribe) => subscribe((_, e) => Filter = e.Value);
}
