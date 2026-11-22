using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The New agent form's keys in the real window (#3384): N on the Agents page opens the form and not the todo box, and the open form keeps the page's single keys to itself.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class NewAgentKeysFramesTests : FrameHost
{
    private static void Press(Window window, Key key, PhysicalKey physical)
    {
        window.KeyPress(key, RawInputModifiers.None, physical, keySymbol: null);
        Dispatcher.UIThread.RunJobs();
    }

    private async Task<(ShellWindow Window, ShellViewModel Shell)> OnAgents()
    {
        Server.SessionAnswer = RouteAnswer.Api("sessions", "fixture-three-providers");
        var (window, shell) = await Connected(width: 1280, height: 900);
        shell.GoTo(PageId.Agents);
        await Until(window, () => shell.Agents.Groups.Count > 0);
        Settle(window);
        _ = window.Focus();
        return (window, shell);
    }

    [Fact]
    public Task N_on_the_Agents_page_opens_the_New_agent_form_and_not_the_todo_box_3384() => OnUi(async () =>
    {
        var (window, shell) = await OnAgents();

        Press(window, Key.N, PhysicalKey.N);

        Assert.Equal((true, false), (shell.NewAgent.IsOpen, shell.QuickAdd.IsOpen));
    });

    [Fact]
    public Task N_on_another_page_still_opens_the_todo_box_3384() => OnUi(async () =>
    {
        var (window, shell) = await Connected(width: 1280, height: 900);
        shell.GoTo(PageId.Home);
        Settle(window);
        _ = window.Focus();

        Press(window, Key.N, PhysicalKey.N);

        Assert.Equal((false, true), (shell.NewAgent.IsOpen, shell.QuickAdd.IsOpen));
    });
}
