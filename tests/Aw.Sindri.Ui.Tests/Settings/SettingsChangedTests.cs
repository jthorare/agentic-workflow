using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Settings;

namespace Aw.Sindri.Ui.Tests.Settings;

/// <summary>#3362: a setting that differs from its default shows a changed mark and can be reset.</summary>
[Lane(TestLane.Unit)]
public sealed class SettingsChangedTests
{
    private readonly InMemoryPreferencesStore _store = new();
    private readonly FakeMotionPreference _os = new();

    private SettingsViewModel Settings() => new(new Preferences(_store), _os, static _ => { });

    [Fact]
    public void Nothing_is_changed_at_the_defaults()
    {
        using var settings = Settings();

        Assert.False(settings.TextScaleChanged);
        Assert.False(settings.ReduceMotionChanged);
        Assert.Equal(0, settings.ChangedCount);
    }

    [Fact]
    public void A_new_text_size_and_the_motion_switch_are_each_marked_changed()
    {
        using var settings = Settings();

        settings.TextScale = 1.2;
        settings.ReduceMotion = true;

        Assert.True(settings.TextScaleChanged);
        Assert.True(settings.ReduceMotionChanged);
        Assert.Equal(2, settings.ChangedCount);
    }

    [Fact]
    public void Reset_puts_each_setting_back_and_clears_its_mark()
    {
        using var settings = Settings();
        settings.TextScale = 1.2;
        settings.ReduceMotion = true;

        settings.ResetTextScale();
        settings.ResetReduceMotion();

        Assert.Equal(1.0, settings.TextScale);
        Assert.False(settings.ReduceMotion);
        Assert.Equal(0, settings.ChangedCount);
    }

    [Fact]
    public void Changing_a_setting_raises_changed_for_its_mark()
    {
        using var settings = Settings();
        var raised = new List<string?>();
        settings.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        settings.TextScale = 1.1;

        Assert.Contains(nameof(SettingsViewModel.TextScaleChanged), raised, StringComparer.Ordinal);
        Assert.Contains(nameof(SettingsViewModel.ChangedCount), raised, StringComparer.Ordinal);
    }

    [Fact]
    public void This_app_goes_first_when_the_page_has_no_checklist()
    {
        using var settings = Settings();

        Assert.True(settings.ThisAppFirst);
    }
}
