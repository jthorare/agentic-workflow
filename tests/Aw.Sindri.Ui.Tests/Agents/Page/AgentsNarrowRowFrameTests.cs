using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Terminal;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.Agents;

namespace Aw.Sindri.Ui.Tests.Agents.Page;

/// <summary>The Agents list at 1440 px with the live pane open (#4162 screenshot): a row's title gets real width, the provider word is gone.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class AgentsNarrowRowFrameTests : FrameHost
{
    private const string ReadyJson = "{\"t\":\"ready\",\"v\":1,\"host\":{\"socket\":\"s\",\"session\":\"x\",\"pane\":\"%3\"}}";
    private const string WithPane = "aaaaaaaa-0000-4000-8000-000000000001";
    private const double LeastTitleWidth = 110;

    [Fact]
    public Task With_the_pane_open_a_rows_title_is_wide_enough_to_read_and_the_provider_word_is_hidden() => OnUi(async () =>
    {
        Terminal = new FakeTerminalConnector
        {
            Script = [ReadyJson, FakeTerminalConnector.FixtureOut().First(m => TerminalSocketMessages.Parse(m).Kind == TerminalServerKind.Frame)],
            StaysOpen = true,
        };
        var (window, shell) = await Connected(width: 1440);
        shell.GoTo(PageId.Agents);
        await Until(window, () => shell.Agents.Drawn > 0, "the Agents page never read the list");
        for (var i = 0; i < 6 && shell.Agents.SelectedKey?.Contains(WithPane, StringComparison.Ordinal) != true; i++)
        {
            window.KeyPress(Key.J, RawInputModifiers.None, PhysicalKey.J, keySymbol: "j");
            Dispatcher.UIThread.RunJobs();
        }

        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, keySymbol: null);
        Dispatcher.UIThread.RunJobs();
        await Until(window, () => shell.Agents.IsTerminalOpen, "the pane never opened");
        Settle(window);

        var rows = window.GetVisualDescendants().OfType<SessionRow>().Where(r => r.IsEffectivelyVisible).ToList();
        Assert.NotEmpty(rows);
        Assert.All(rows, row =>
        {
            Assert.False(Named<TextBlock>(row, "SessionRowProvider").IsEffectivelyVisible);
            var title = Named<TextBlock>(row, "SessionRowTitle");
            // Readable = shown whole (the button hugs short text, so it can be under 110 px) or given at least 110 px (a long title is trimmed, never squeezed to a few characters).
            var whole = UntrimmedWidth(title);
            Assert.True(
                whole <= title.Bounds.Width + 0.5 || title.Bounds.Width >= LeastTitleWidth,
                $"the title is {title.Bounds.Width.ToString(CultureInfo.InvariantCulture)} px wide for {whole.ToString(CultureInfo.InvariantCulture)} px of text");
            Assert.Same(TextTrimming.PrefixCharacterEllipsis, title.TextTrimming);   // 8 characters, the ellipsis, then the tail
        });
    });

    private static double UntrimmedWidth(TextBlock title)
    {
        using var layout = new TextLayout(title.Text ?? string.Empty, new Typeface(title.FontFamily, title.FontStyle, title.FontWeight), title.FontSize, foreground: null);
        return layout.Width;
    }

    [Fact]
    public Task Without_the_pane_the_title_still_ends_with_the_ellipsis() => OnUi(async () =>
    {
        var (window, shell) = await Connected(width: 1440);
        shell.GoTo(PageId.Agents);
        await Until(window, () => shell.Agents.Drawn > 0, "the Agents page never read the list");
        Settle(window);

        var rows = window.GetVisualDescendants().OfType<SessionRow>().Where(r => r.IsEffectivelyVisible).ToList();
        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.Same(TextTrimming.CharacterEllipsis, Named<TextBlock>(row, "SessionRowTitle").TextTrimming));
    });
}
