using Avalonia;
using Avalonia.Headless;
using Aw.UiDriver.Avalonia.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace Aw.UiDriver.Avalonia.Tests;

/// <summary>The headless app the driver tests run in: Skia drawing on, so a screenshot has pixels, and no theme, so nothing but the test's own controls is on screen.</summary>
public static class TestAppBuilder
{
    /// <summary>Builds the app for the headless session.</summary>
    /// <returns>The builder.</returns>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Application>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
