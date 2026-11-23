using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The Agents list in a window (#3082): the pinned Needs you group on top of the list and the search box in the head; the / key puts focus in it.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class AgentsNeedsYouFramesTests : FrameHost
{
    [Fact]
    public Task The_Needs_you_group_is_first_with_its_count_and_the_search_box_takes_focus_on_slash_3082() => OnUi(async () =>
    {
        Server.SessionAnswer = RouteAnswer.Api("sessions", "fixture-three-providers");
        var (window, shell) = await Connected(width: 1280, height: 900);
        shell.GoTo(PageId.Agents);
        await Until(window, () => shell.Agents.Groups.Count > 0);
        Settle(window);

        var search = Named<TextBox>(window, "AgentsSearch");
        _ = shell.Agents.Press("/");
        Settle(window);

        Assert.Equal("Needs you (1)", shell.Agents.Groups[0].Header);
        Assert.True(search.IsEffectivelyVisible);
        Assert.Same(search, window.FocusManager?.GetFocusedElement());
        Assert.NotNull(window.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.IsEffectivelyVisible && c.DataContext is ViewModels.Agents.SessionGroupViewModel g && string.Equals(g.Name, "Needs you", StringComparison.Ordinal)));
    });

    [Fact]
    public Task Pressing_slash_on_the_page_focuses_the_search_box_without_typing_the_slash_into_it_3082() => OnUi(async () =>
    {
        Server.SessionAnswer = RouteAnswer.Api("sessions", "fixture-three-providers");
        var (window, shell) = await Connected(width: 1280, height: 900);
        shell.GoTo(PageId.Agents);
        await Until(window, () => shell.Agents.Groups.Count > 0);
        Settle(window);
        var search = Named<TextBox>(window, "AgentsSearch");

        // The key and its text, as a keyboard sends them: the text goes to whatever has focus once the key is done.
        window.KeyPress(Key.OemQuestion, RawInputModifiers.None, PhysicalKey.Slash, keySymbol: "/");
        window.KeyTextInput("/");
        await Until(window, () => search.IsFocused, "the / key never put focus in the Agents search box");
        Settle(window);

        Assert.Equal(string.Empty, search.Text ?? string.Empty);
        Assert.Equal(string.Empty, shell.Agents.SearchText);
    });
}
