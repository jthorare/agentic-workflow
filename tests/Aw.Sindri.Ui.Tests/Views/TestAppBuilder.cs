using Avalonia;
using Avalonia.Headless;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>
/// The app's theme under Avalonia's headless platform with headless drawing: no Skia, so a window builds and lays out without
/// rendering a frame (the integration lane's budget, LANE-PKG-001). A test that reads pixels lives in Aw.Sindri.Ui.Rendering.Tests.
/// The key-frame tests save their PNGs only when a human asks for them: set AW_UI_FRAMES=1 and the project draws with Skia.
/// </summary>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
    {
        var draw = string.Equals(Environment.GetEnvironmentVariable("AW_UI_FRAMES"), "1", StringComparison.Ordinal);
        var builder = AppBuilder.Configure<SindriApp>();
        if (draw)
        {
            builder = builder.UseSkia();
        }

        return builder
            .WithInterFont()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = !draw });
    }
}
