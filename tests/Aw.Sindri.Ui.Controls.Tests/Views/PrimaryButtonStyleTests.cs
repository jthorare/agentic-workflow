using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;

namespace Aw.Sindri.Ui.Controls.Tests.Views;

/// <summary>The recommended option is drawn primary (real-app re-proof, NB2): a command button with the primary class is filled with the primary token.</summary>
[Lane(TestLane.Integration)]
public sealed class PrimaryButtonStyleTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static IBrush? Fill(Window window, string classes)
    {
        var button = new Button { Content = "Accept" };
        foreach (var name in classes.Split(' '))
        {
            button.Classes.Add(name);
        }

        window.Content = button;
        window.Show();
        Dispatcher.UIThread.RunJobs();
        button.ApplyTemplate();
        return button.GetVisualDescendants().OfType<ContentPresenter>().First(p => p.Name == "PART_ContentPresenter").Background;
    }

    [Fact]
    public async Task A_primary_command_button_is_filled_differently_from_a_secondary_one()
    {
        var (plain, primary) = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(PrimaryButtonStyleTests).Assembly)
            .Dispatch(() =>
            {
                var window = new Window();
                var result = (Fill(window, "command-control"), Fill(window, "command-control primary"));
                window.Close();
                return result;
            }, Token);

        Assert.NotEqual(((ISolidColorBrush)plain!).Color, ((ISolidColorBrush)primary!).Color);
        Assert.Equal(Color.Parse("#5e6ad2"), ((ISolidColorBrush)primary).Color);
    }
}
