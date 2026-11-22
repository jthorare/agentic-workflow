using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Aw.Kernel.Testing;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>The failed-open lines are SelectableTextBlocks with the classes "caption error" (#3714). The error style must reach them: a type selector matches the exact type, so a TextBlock-only rule leaves the text in the body colour.</summary>
[Lane(TestLane.Unit)]
public sealed class FailedOpenLineColourTests
{
    [Fact]
    public async Task A_selectable_text_block_with_the_error_class_takes_the_error_colour()
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(FailedOpenLineColourTests).Assembly);

        var (actual, expected) = await session.Dispatch(
            () =>
            {
                var line = new SelectableTextBlock { Text = "This file isn't on this machine." };
                line.Classes.Add("caption");
                line.Classes.Add("error");
                var window = new Window { Content = line };
                window.Show();
                var error = line.FindResource("Brush.Error") as ISolidColorBrush ?? throw new InvalidOperationException("Brush.Error is not a solid brush");
                var colour = (line.Foreground as ISolidColorBrush)?.Color;
                window.Close();
                return (colour, error.Color);
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, actual);
    }
}
