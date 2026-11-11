using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>The search field says the key the window answers to: ⌘K on macOS, Ctrl+K elsewhere (ShellWindow's command modifier). #2547.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellTextSearchKeyTests
{
    [Fact]
    public void On_macOS_the_search_key_is_Command_K_with_the_Command_icon()
    {
        var text = new ShellText(macOS: true);

        Assert.Equal(("⌘K", "Search or run a command (⌘K)", true, string.Empty, "K"), (text.SearchKey, text.SearchTip, text.ShowCommandIcon, text.SearchKeyModifier, text.SearchKeyLetter));
    }

    [Fact]
    public void Elsewhere_the_search_key_is_Ctrl_K_spelled_out_with_no_Command_icon()
    {
        var text = new ShellText(macOS: false);

        Assert.Equal(("Ctrl+K", "Search or run a command (Ctrl+K)", false, "Ctrl+", "K"), (text.SearchKey, text.SearchTip, text.ShowCommandIcon, text.SearchKeyModifier, text.SearchKeyLetter));
    }
}
