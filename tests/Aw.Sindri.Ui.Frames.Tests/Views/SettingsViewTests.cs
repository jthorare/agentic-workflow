using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Styling;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Settings;
using Aw.Sindri.Ui.Views;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// The Settings page under the headless platform, applying to the running app's theme (UI spec §11.4, §14.2, §18.2
/// "Reduced motion: the in-app switch; key frames before and after"). Each test puts the theme back as it found it.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class SettingsViewTests
{
    private static readonly string[] _motionKeys = ["Motion.Fast", "Motion.Base", "Motion.Pulse"];
    private readonly InMemoryPreferencesStore _store = new();
    private readonly Preferences _preferences;
    private readonly FakeMotionPreference _os = new();
    private readonly List<Window> _windows = [];

    public SettingsViewTests()
    {
        _preferences = new Preferences(_store);
    }

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(SettingsViewTests).Assembly)
            .Dispatch(async () =>
            {
                try
                {
                    await body();
                }
                finally
                {
                    // Put the theme back and settle before the session tears down: a layout pass left queued would run
                    // after the headless session has reset the app (fonts included) and fail there.
                    SettingsTheme.For(Resources)(new AppearanceSettings(1.0, ReduceMotion: false));
                    _windows.ForEach(w => w.Close());
                    Dispatcher.UIThread.RunJobs();
                }

                return true;
            }, TestContext.Current.CancellationToken);

    private static IResourceDictionary Resources => Application.Current!.Resources; // why: the headless session starts the app

    private static object? Resource(string key) =>
        Application.Current!.TryGetResource(key, ThemeVariant.Dark, out var value) ? value : null; // why: as above

    private async Task<(Window Window, SettingsViewModel Settings)> Show()
    {
        var settings = new SettingsViewModel(_preferences, _os, SettingsTheme.For(Resources));
        await settings.Load(TestContext.Current.CancellationToken);
        var window = new Window { Width = 900, Height = 600, Content = new SettingsView { DataContext = settings } };
        _windows.Add(window);
        SettingsTheme.Follow(window, settings);
        window.Show();
        Settle(window);
        return (window, settings);
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static T Find<T>(Window window, string name)
        where T : Control => window.GetLogicalDescendants().OfType<T>().Single(c => c.Name == name);

    private static void Capture(Window window, string name) => FrameFile.Save(window, name);

    [Fact]
    public Task The_model_choice_line_shows_its_state_the_sentence_and_a_copy_button() => OnUi(() =>
    {
        var clipboard = new FakeClipboard();
        var line = new ModelChoiceViewModel(clipboard, TimeProvider.System);
        line.Show(new ProfileView("a", "a", ChangedSinceApproval: false, Problem: null, "/home/thor/profile.yaml", NeedsApproval: false, ModelPick: "on"));
        using var settings = new SettingsViewModel(_preferences, _os, SettingsTheme.For(Resources), setup: null, modelChoice: line);
        var window = new Window { Width = 900, Height = 600, Content = new SettingsView { DataContext = settings } };
        _windows.Add(window);
        window.Show();
        Settle(window);

        Assert.Equal("Model choice: on", Find<TextBlock>(window, "ModelChoiceLine").Text);
        Assert.StartsWith("Jev reads each step's brief", Find<TextBlock>(window, "ModelChoiceSentence").Text, StringComparison.Ordinal);
        var copy = Find<Button>(window, "ModelChoiceCopy");
        Assert.Equal("Copy: Model choice", AutomationProperties.GetName(copy));
        Assert.True(copy.IsEffectivelyVisible);
        return Task.CompletedTask;
    });

    [Fact]
    public Task The_switch_turns_every_motion_duration_to_zero_and_back() => OnUi(async () =>
    {
        var (window, settings) = await Show();
        using (settings)
        {
            var toggle = Find<ToggleSwitch>(window, "ReduceMotionSwitch");
            Assert.False(toggle.IsChecked);
            Assert.NotEmpty(toggle.KnobTransitions ?? []);
            Assert.False(Find<TextBlock>(window, "SystemMotionText").IsEffectivelyVisible);
            Assert.Equal(TimeSpan.FromMilliseconds(180), Resource("Motion.Base"));
            Capture(window, "settings-motion-off");

            toggle.IsChecked = true;
            Settle(window);
            await _preferences.Flush();

            Assert.True(settings.ReduceMotion);
            Assert.Empty(toggle.KnobTransitions ?? []);
            Assert.All(_motionKeys, key => Assert.Equal(TimeSpan.Zero, Resource(key)));
            Assert.True((await new Preferences(_store).Load(TestContext.Current.CancellationToken)).ReduceMotion);
            Capture(window, "settings-motion-on");

            toggle.IsChecked = false;
            Settle(window);

            Assert.Equal(TimeSpan.FromMilliseconds(120), Resource("Motion.Fast"));
            Assert.Equal(TimeSpan.FromMilliseconds(1600), Resource("Motion.Pulse"));
        }
    });

    [Fact]
    public Task The_OS_setting_reduces_motion_and_says_so() => OnUi(async () =>
    {
        var (window, settings) = await Show();
        using (settings)
        {
            _os.Set(reduce: true);
            Settle(window);

            Assert.False(Find<ToggleSwitch>(window, "ReduceMotionSwitch").IsChecked);
            Assert.True(Find<TextBlock>(window, "SystemMotionText").IsEffectivelyVisible);
            Assert.Equal(Labels.SystemReducesMotion, Find<TextBlock>(window, "SystemMotionText").Text);
            Assert.Equal(TimeSpan.Zero, Resource("Motion.Base"));
            Capture(window, "settings-system-reduces-motion");
        }
    });

    [Fact]
    public Task Reduced_motion_stops_stock_controls_own_transitions_anywhere_in_the_window() => OnUi(async () =>
    {
        var settings = new SettingsViewModel(_preferences, _os, SettingsTheme.For(Resources));
        using (settings)
        {
            await settings.Load(TestContext.Current.CancellationToken);
            var elsewhere = new ToggleSwitch { Name = "Elsewhere" };
            var window = new Window { Width = 900, Height = 600, Content = elsewhere };
            _windows.Add(window);
            SettingsTheme.Follow(window, settings);
            window.Show();
            Settle(window);
            Assert.NotEmpty(elsewhere.KnobTransitions ?? []);

            settings.ReduceMotion = true;
            Settle(window);
            await _preferences.Flush();

            Assert.Empty(elsewhere.KnobTransitions ?? []);

            settings.ReduceMotion = false;
            Settle(window);

            Assert.NotEmpty(elsewhere.KnobTransitions ?? []);
        }
    });

    [Fact]
    public Task The_slider_scales_the_type_ramp() => OnUi(async () =>
    {
        var (window, settings) = await Show();
        using (settings)
        {
            Find<Slider>(window, "TextScaleSlider").Value = 1.2;
            Settle(window);

            Assert.Equal("120%", Find<TextBlock>(window, "TextScaleValueText").Text);
            Assert.Equal(13 * 1.2, (double)Resource("Type.Body.Size")!, 6); // why: the token exists (ThemeResourcesTests)
            Capture(window, "settings-text-120");
        }
    });

    [Fact]
    public Task Every_control_has_an_automation_name() => OnUi(async () =>
    {
        var (window, settings) = await Show();
        using (settings)
        {
            var named = window.GetLogicalDescendants().OfType<Control>().Where(c => !string.IsNullOrEmpty(c.Name)).ToList();

            Assert.Contains(named, c => c is ToggleSwitch);
            Assert.Contains(named, c => c is Slider);
            Assert.All(named.Where(c => c.IsEffectivelyVisible), c => Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(c)), c.Name));
            Assert.Equal(Labels.ReduceMotion, AutomationProperties.GetName(Find<ToggleSwitch>(window, "ReduceMotionSwitch")));
            Assert.Equal(Labels.TextSize, AutomationProperties.GetName(Find<Slider>(window, "TextScaleSlider")));
        }
    });
}
