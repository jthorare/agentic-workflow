using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Agents;
using Aw.Sindri.Ui.Views.Agents;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The New agent form hosted alone at 1000 wide (DG2 frame f1-form-default-1000): it shows, the counter reads "0 / 8,000" and the two agents are listed.</summary>
[Lane(TestLane.Integration)]
public sealed class NewAgentFormFrameTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_form_at_1000_wide_shows_the_empty_counter_and_lists_two_agents()
    {
        var result = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(NewAgentFormFrameTests).Assembly).Dispatch(() =>
        {
            var form = new NewAgentFormViewModel(
                () => new Uri("http://127.0.0.1:7480"),
                new FakeNewAgentRoute(),
                new FakeClipboard(),
                [new RepoChoice("sindri", Onboarded: true)],
                new NewAgentDefaults(Repo: null, Agent: null),
                () => "act000001");
            form.Open();
            var view = new NewAgentFormView { DataContext = form };
            var window = new Window { Width = 1000, Height = 900, Content = view };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            try
            {
                var agents = view.FindControl<ComboBox>("NewAgentAgent")!;
                return (view.IsEffectivelyVisible, form.PromptCount, agents.ItemCount, view.FindControl<Button>("NewAgentStart")!.IsEnabled);
            }
            finally
            {
                window.Close();
            }
        }, Token);

        Assert.Equal((true, "0 / 8,000", 2, true), result);
    }
}
