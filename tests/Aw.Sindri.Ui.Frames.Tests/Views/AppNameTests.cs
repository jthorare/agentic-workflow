using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Aw.Kernel.Testing;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// The app's name as macOS shows it (the menu bar's app menu, "Hide Sindri", the Dock): Avalonia.Native titles the app from
/// Application.Name and, without an app menu of the app's own, adds an "About Avalonia" item.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class AppNameTests
{
    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(AppNameTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    [Fact]
    public async Task The_app_is_named_Sindri() =>
        Assert.Equal("Sindri", await OnUi(() => Application.Current!.Name));

    [Fact]
    public async Task The_app_menu_is_the_apps_own_with_no_About_Avalonia()
    {
        var headers = await OnUi(() => NativeMenu.GetMenu(Application.Current!)?.Items.OfType<NativeMenuItem>().Select(i => i.Header).ToList());

        Assert.NotNull(headers);
        Assert.DoesNotContain(headers, h => h?.Contains("Avalonia", StringComparison.Ordinal) == true);
    }
}
