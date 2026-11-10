using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Aw.Kernel.Testing;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The shared status chip (#2545): one Border.status-chip per Sindri state, its fill, border and text resolved from the Brush.Status.* tokens, in the shape every page shares.</summary>
[Lane(TestLane.Integration)]
public sealed class StatusChipTests
{
    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(StatusChipTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    public static TheoryData<string, string> Variants() => new()
    {
        { "ok", "Ok" }, { "running", "Running" }, { "waiting", "Waiting" }, { "attention", "Attention" },
        { "error", "Error" }, { "neutral", "Neutral" }, { "draft", "Draft" },
    };

    private static IBrush? Token(string key) => Application.Current!.FindResource(key) as IBrush; // why: the headless session starts the app

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task A_chip_resolves_its_fill_border_and_text_brushes_from_its_variant(string variant, string token)
    {
        var (bg, border, fg, radius, height, thickness) = await OnUi(() =>
        {
            var text = new TextBlock { Text = variant };
            var chip = new Border { Child = text };
            chip.Classes.Add("status-chip");
            chip.Classes.Add(variant);
            var window = new Window { Content = chip };
            window.Show();
            window.UpdateLayout();
            return (chip.Background, chip.BorderBrush, text.Foreground, chip.CornerRadius, chip.Bounds.Height, chip.BorderThickness);
        });

        _ = await OnUi(() =>
        {
            Assert.Same(Token($"Brush.Status.{token}.Bg"), bg);
            Assert.Same(Token($"Brush.Status.{token}.Border"), border);
            Assert.Same(Token($"Brush.Status.{token}.Fg"), fg);
            Assert.Equal(new CornerRadius(4), radius);
            Assert.Equal(20, height);
            Assert.Equal(new Thickness(1), thickness);
            return 0;
        });
    }
}
