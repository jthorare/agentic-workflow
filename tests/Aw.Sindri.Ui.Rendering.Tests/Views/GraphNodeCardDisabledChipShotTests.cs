using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Aw.Sindri.Ui.Views.Workflows;

namespace Aw.Sindri.Ui.Rendering.Tests.Views;

/// <summary>
/// Shot frames for #3648: a disabled graph card next to an enabled one, both with a "Succeeded" status chip. The app has no way to
/// disable a step yet, so the pair is built here. Saves PNGs only when AW_SHOTS_OUT names a folder; otherwise it returns at once.
/// Files: card-pair-crop.png (just the pair) and card-pair-1440.png (a 1440 px wide frame). Heavy lane: the normal lanes skip it.
/// </summary>
[Lane(TestLane.Heavy)]
[Collection(HeadlessWindows.Name)]
public sealed class GraphNodeCardDisabledChipShotTests
{
    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(GraphNodeCardDisabledChipShotTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    private static GraphNodeCard Card(string title, bool enabled) =>
        new() { Title = title, Kind = "run_agent", StatusWord = "Succeeded", StatusTone = "success", StatusGlyph = "✓", IsEnabled = enabled, Margin = new Thickness(16) };

    private static void Shoot(string folder, string name, double width, double height)
    {
        var pair = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { Card("Enabled step", enabled: true), Card("Disabled step", enabled: false) },
        };
        var stage = new Border { Background = (IBrush)Application.Current!.FindResource("Brush.Canvas")!, Child = pair };
        var window = new Window { Width = width, Height = height, Content = stage };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        using var bitmap = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("no frame: this project draws with Skia");
        bitmap.Save(Path.Combine(folder, name), new PngBitmapEncoderOptions());
        window.Close();
    }

    [Fact]
    public async Task Save_the_enabled_and_disabled_card_pair() => await OnUi(() =>
    {
        var folder = Environment.GetEnvironmentVariable("AW_SHOTS_OUT");
        if (string.IsNullOrEmpty(folder))
        {
            return true;
        }

        _ = Directory.CreateDirectory(folder);
        var crop = (2 * (WorkflowGraphViewModel.CardWidth + 32)) + 16 + 32;
        Shoot(folder, "card-pair-crop.png", crop, WorkflowGraphViewModel.CardHeight + 64);
        Shoot(folder, "card-pair-1440.png", 1440, 900);
        return true;
    });
}
