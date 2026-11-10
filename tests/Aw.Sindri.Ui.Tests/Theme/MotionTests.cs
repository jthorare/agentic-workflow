using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Theme;

namespace Aw.Sindri.Ui.Tests.Theme;

/// <summary>
/// UI spec §11.4, §14.3: reduced motion sets every Motion.* duration to 0, and turning it off restores the tokens' values.
/// Integration lane, on the headless session's UI thread: an Avalonia resource dictionary built on a test thread binds
/// Avalonia's dispatcher to that thread and breaks the headless tests that run after it.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class MotionTests
{
    private static readonly string[] _keys = ["Motion.Instant", "Motion.Fast", "Motion.Hover", "Motion.Base", "Motion.Pulse", "Motion.Spin"];
    private static readonly TokenFile _tokens = TokenFile.Load();

    private static TimeSpan Token(string key) => TimeSpan.Parse(_tokens.Resources[key].Value.Trim(), CultureInfo.InvariantCulture);

    // The app's resources as SindriApp.axaml builds them: its own dictionary with the tokens merged in.
    private static ResourceDictionary AppResources()
    {
        var tokens = new ResourceDictionary();
        foreach (var key in _keys)
        {
            tokens[key] = Token(key);
        }

        var app = new ResourceDictionary();
        app.MergedDictionaries.Add(tokens);
        return app;
    }

    private static async Task OnUi(Action body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(MotionTests).Assembly)
            .Dispatch(() =>
            {
                body();
                return true;
            }, TestContext.Current.CancellationToken);

    private static TimeSpan Resolved(ResourceDictionary resources, string key) =>
        Assert.IsType<TimeSpan>(resources.TryGetResource(key, theme: null, out var value) ? value : null);

    [Fact]
    public Task Reduced_motion_sets_every_duration_to_zero() => OnUi(() =>
    {
        var resources = AppResources();

        Motion.Apply(resources, reduce: true);

        Assert.All(_keys, key => Assert.Equal(TimeSpan.Zero, Resolved(resources, key)));
    });

    [Fact]
    public Task Motion_back_on_restores_the_token_durations() => OnUi(() =>
    {
        var resources = AppResources();
        Motion.Apply(resources, reduce: true);

        Motion.Apply(resources, reduce: false);

        Assert.All(_keys, key => Assert.Equal(Token(key), Resolved(resources, key)));
        Assert.Equal(TimeSpan.FromMilliseconds(180), Resolved(resources, "Motion.Base"));
    });
}
