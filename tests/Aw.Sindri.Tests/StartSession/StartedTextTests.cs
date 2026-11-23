using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Features.StartSession;
using Aw.Sindri.Host.Commands;

namespace Aw.Sindri.Tests.StartSession;

/// <summary>#3918: 'sindri agents start' prints the attach line as Core's TmuxAttachLine builds it: every part shell-quoted when it needs it, the session as an exact match, '=&lt;session&gt;'.</summary>
public sealed class StartedTextTests
{
    [Fact]
    public void A_plain_start_prints_the_attach_line_with_the_session_quoted_as_an_exact_match_3918()
    {
        var started = new StartedSession(SessionId.From("sindri-k3j9x0a1"), ActId.From("k3j9x0a1"), "starting", new StartedHost("sindri-launch", "sindri-k3j9x0a1", "%3"));

        var text = AgentStartVerbs.StartedText(started);

        Assert.Equal("started sindri-k3j9x0a1 (act id k3j9x0a1, pane %3)\nattach: tmux -L sindri-launch attach -t '=sindri-k3j9x0a1'\n", text);
    }

    [Fact]
    public void A_named_server_line_single_quotes_every_shell_special_part_3918() =>
        Assert.Equal("""tmux -L 'my server' attach -t '=it'\''s "x";$y'""", TmuxAttachLine.NamedLine("my server", "it's \"x\";$y"));
}
