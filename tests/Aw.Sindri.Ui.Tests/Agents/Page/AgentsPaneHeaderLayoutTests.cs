using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Terminal;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.Agents;

namespace Aw.Sindri.Ui.Tests.Agents.Page;

/// <summary>The page header's filter row belongs to the list column: with the live pane open nothing of it reaches the pane (#3728 screenshot at 1440 px).</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class AgentsPaneHeaderLayoutTests : FrameHost
{
    private const string ReadyJson = "{\"t\":\"ready\",\"v\":1,\"host\":{\"socket\":\"s\",\"session\":\"x\",\"pane\":\"%3\"}}";
    private const string WithPane = "aaaaaaaa-0000-4000-8000-000000000001";

    private static readonly string[] FilterNames = ["ProviderFilter", "StateFilter", "RepoFilter"];

    [Theory]
    [InlineData(1440)]
    [InlineData(1280)]
    public Task Filters_stay_inside_the_list_column_while_the_pane_is_open(int width) => OnUi(async () =>
    {
        Terminal = new FakeTerminalConnector
        {
            Script = [ReadyJson, FakeTerminalConnector.FixtureOut().First(m => TerminalSocketMessages.Parse(m).Kind == TerminalServerKind.Frame)],
            StaysOpen = true,
        };
        var (window, shell) = await Connected(width: width);
        shell.GoTo(PageId.Agents);
        await Until(window, () => shell.Agents.Drawn > 0, "the Agents page never read the list");
        for (var i = 0; i < 6 && shell.Agents.SelectedKey?.Contains(WithPane, StringComparison.Ordinal) != true; i++)
        {
            window.KeyPress(Key.J, RawInputModifiers.None, PhysicalKey.J, symbol: "j");
            Dispatcher.UIThread.RunJobs();
        }

        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, symbol: null);
        Dispatcher.UIThread.RunJobs();
        await Until(window, () => shell.Agents.IsTerminalOpen, "the pane never opened");
        Settle(window);

        var paneLeft = Named<TerminalView>(window, "TerminalPanel").TranslatePoint(new Point(x: 0, y: 0), window)!.Value.X;
        foreach (var name in FilterNames)
        {
            var filter = Named<ComboBox>(window, name);
            var right = filter.TranslatePoint(new Point(x: filter.Bounds.Width, y: 0), window)!.Value.X;
            Assert.True(filter.IsVisible && filter.Bounds.Width > 0, $"{name} is not shown");
            Assert.True(right <= paneLeft, string.Create(CultureInfo.InvariantCulture, $"{name} ends at {right}, the pane starts at {paneLeft}"));
        }
    });
}
