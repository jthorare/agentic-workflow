using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The danger tone of a command control's confirm (#3234): a filled ErrorFill button with white text at AA, so the Remove confirm is CommandControl's own.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class DangerButtonTests
{
    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(DangerButtonTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    [Fact]
    public async Task A_destructive_primary_button_is_filled_with_ErrorFill_and_white_text() => await OnUi(() =>
    {
        var danger = new Button { Content = "Remove step" };
        danger.Classes.Add("command-control");
        danger.Classes.Add("destructive");
        danger.Classes.Add("primary");
        var window = new Window { Content = danger };
        window.Show();
        window.UpdateLayout();

        var presenter = danger.GetVisualDescendants().OfType<ContentPresenter>().First();

        Assert.Equal(Color.Parse("#c93c3c"), Assert.IsType<ISolidColorBrush>(presenter.Background, exactMatch: false).Color);
        Assert.Equal(Color.Parse("#ffffff"), Assert.IsType<ISolidColorBrush>(presenter.Foreground, exactMatch: false).Color);
        window.Close();
        return true;
    });

    [Fact]
    public async Task The_confirm_button_of_a_command_control_is_the_danger_primary() => await OnUi(() =>
    {
        var control = new CommandControl();
        var window = new Window { Content = control };
        window.Show();

        var confirm = control.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "ConfirmButton");

        Assert.Contains("destructive", confirm.Classes, StringComparer.Ordinal);
        Assert.Contains("primary", confirm.Classes, StringComparer.Ordinal);
        window.Close();
        return true;
    });
}
