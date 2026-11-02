using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Notifications;

namespace Aw.Sindri.Tests.Notifications;

/// <summary>What shows a banner, and what a click on it does: terminal-notifier runs 'sindri-ui &lt;link&gt;'; without it, or without a safe link, no banner is shown (osascript's click opens Script Editor).</summary>
[Lane(TestLane.Unit)]
public sealed class NotifierCommandTests
{
    private const string Ui = "/opt/sindri/sindri-ui";
    private const string Notifier = "/opt/homebrew/bin/terminal-notifier";

    [Fact]
    public void With_terminal_notifier_and_a_link_a_click_runs_the_app_with_the_link()
    {
        var command = NotifierCommand.For("Sindri: ITM-01JN2/design/1", "Waiting for you.", "workflow/ITM-01JN2/design/1", Notifier, Ui);

        Assert.Equal(Notifier, command.File);
        Assert.Equal(["-title", "Sindri: ITM-01JN2/design/1", "-message", "Waiting for you.", "-execute", "'/opt/sindri/sindri-ui' workflow/ITM-01JN2/design/1"], command.Arguments, StringComparer.Ordinal);
    }

    [Fact]
    public void A_path_with_a_quote_is_quoted_for_the_shell()
    {
        var command = NotifierCommand.For("t", "x", "workflow/a", Notifier, "/Users/o'neil/sindri-ui");

        Assert.Equal("'/Users/o'\\''neil/sindri-ui' workflow/a", command.Arguments[^1]);
    }

    [Fact]
    public void The_app_is_the_one_AW_SINDRI_UI_names_else_sindri_ui_next_to_the_program()
    {
        Assert.Equal("/custom/ui", NotifierCommand.UiBinary(_ => "/custom/ui", "/opt/sindri"));
        Assert.Equal(Path.Combine("/opt/sindri", "sindri-ui"), NotifierCommand.UiBinary(_ => null, "/opt/sindri"));
    }

    [Fact]
    public void A_banner_is_shown_only_when_its_click_opens_the_app()
    {
        Assert.True(NotifierCommand.Shows("needs-you/desk:WFI-a", Notifier));
        Assert.False(NotifierCommand.Shows("needs-you/desk:WFI-a", terminalNotifier: null));   // osascript's click opens Script Editor, so no banner
        Assert.False(NotifierCommand.Shows(deepLink: null, Notifier));
        Assert.False(NotifierCommand.Shows("needs-you/x; echo hi", Notifier));
    }
}
