using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Aw.Kernel.Testing;

namespace Aw.Sindri.Ui.Tests.Theme;

/// <summary>Six failed-open and error lines are SelectableTextBlocks with the class "caption" (#3785). A type selector matches the exact type, so a TextBlock-only caption rule leaves them at body size: the caption style must reach a SelectableTextBlock too.</summary>
[Lane(TestLane.Integration)]
public sealed class CaptionSelectableTextTests
{
    [Fact]
    public async Task A_caption_selectable_text_block_renders_at_caption_size_and_colour()
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(CaptionSelectableTextTests).Assembly);

        var (selectable, plain) = await session.Dispatch(
            () =>
            {
                var selectableLine = new SelectableTextBlock { Text = "This file isn't on this machine." };
                selectableLine.Classes.Add("caption");
                var plainLine = new TextBlock { Text = "This file isn't on this machine." };
                plainLine.Classes.Add("caption");
                var panel = new StackPanel();
                panel.Children.Add(selectableLine);
                panel.Children.Add(plainLine);
                var window = new Window { Content = panel };
                window.Show();
                var rendered = (Look.Of(selectableLine), Look.Of(plainLine));
                window.Close();
                return rendered;
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(plain, selectable);
    }

    private sealed record Look(double FontSize, double LineHeight, double LetterSpacing, FontWeight FontWeight, Color? Foreground)
    {
        public static Look Of(TextBlock line) =>
            new(line.FontSize, line.LineHeight, line.LetterSpacing, line.FontWeight, (line.Foreground as ISolidColorBrush)?.Color);
    }
}
