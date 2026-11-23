using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Terminal;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Agents;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Tests.Agents.Terminal;

/// <summary>Scrolling the live pane through tmux history (#3730, #3829): Shift+PageUp and Shift+PageDown as `scroll` messages, a key back to live, no cursor on a frame drawn scrolled back.</summary>
[Lane(TestLane.Unit)]
public sealed class TerminalScrollTests
{
    private const string ReadyJson = "{\"t\":\"ready\",\"v\":1,\"host\":{\"socket\":\"s\",\"session\":\"x\",\"pane\":\"%0\"}}";

    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // A 10x5 frame drawn for `back` rows above the live screen's top, with `hist` rows of tmux history (the socket table of #3730).
    private static string FrameAt(int back, int hist) => string.Create(
        CultureInfo.InvariantCulture,
        $$"""{"t":"frame","seq":1,"rows":["","","","",""],"cursor":{"x":0,"y":0},"w":10,"h":5,"alt":0,"inMode":0,"dead":0,"back":{{back}},"hist":{{hist}}}""");

    // Plays ready and the frames; after frame i is drawn, steps[i] runs on the open socket (the person pressing keys while it is up).
    private static async Task<(TerminalFixture F, FakeTerminalConnector Socket, TerminalSession Session, List<TerminalGrid> Drawn)> Play(IReadOnlyList<string> frames, params Action<TerminalSession>[] steps)
    {
        var f = new TerminalFixture();
        f.View.Open(TerminalFixture.Lane("ideas"), TerminalOpenIntent.Look);
        var socket = new FakeTerminalConnector { Script = [ReadyJson, .. frames] };
        var drawn = new List<TerminalGrid>();
        TerminalSession? session = null;
        session = new TerminalSession(socket, f.View, grid =>
        {
            drawn.Add(grid);
            if (drawn.Count <= steps.Length)
            {
                steps[drawn.Count - 1](session!);
            }
        });
        await session.Run(new TerminalOpen(_api, "claude-code", "ideas", 120, 36), Token);
        return (f, socket, session, drawn);
    }

    [Fact]
    public async Task Shift_PageUp_asks_for_one_screen_further_back_and_Shift_PageDown_stops_at_live()
    {
        var backs = new List<int>();
        void Press(TerminalSession s, int pages)
        {
            _ = s.ScrollPage(pages, Token);
            backs.Add(s.Back);
        }

        var (f, socket, _, _) = await Play([FrameAt(0, 20)], s =>
        {
            Press(s, 1);
            Press(s, 1);
            Press(s, -1);
            Press(s, -1);
            Press(s, -1);
        });

        Assert.Equal([TerminalSocketMessages.Scroll(5), TerminalSocketMessages.Scroll(10), TerminalSocketMessages.Scroll(5), TerminalSocketMessages.Scroll(0)], socket.Sent);
        Assert.Equal([5, 10, 5, 0, 0], backs);
        Assert.Equal(0, f.View.ScrollBack);
    }

    [Fact]
    public async Task Shift_PageUp_is_clamped_to_the_history_the_frame_names_and_past_it_sends_nothing()
    {
        var (_, socket, session, _) = await Play([FrameAt(0, 7)], s =>
        {
            _ = s.ScrollPage(1, Token);
            _ = s.ScrollPage(1, Token);
            _ = s.ScrollPage(1, Token);
        });

        Assert.Equal([TerminalSocketMessages.Scroll(5), TerminalSocketMessages.Scroll(7)], socket.Sent);
        Assert.Equal(7, session.Back);
    }

    [Theory]
    [InlineData("key")]
    [InlineData("text")]
    [InlineData("paste")]
    [InlineData("interrupt")]
    public async Task A_key_text_paste_or_interrupt_puts_the_pane_back_on_live_with_no_scroll_message(string kind)
    {
        var scrolled = -1;
        var (f, socket, session, _) = await Play([FrameAt(0, 20)], s =>
        {
            _ = s.ScrollPage(1, Token);
            scrolled = s.Back;
            Task done = kind switch
            {
                "key" => s.SendKey("Enter", text: null, Token),
                "text" => s.SendKey("A", "a", Token),
                "paste" => s.SendPaste("ls", Token),
                _ => s.Interrupt(Token),
            };
            Assert.True(done.IsCompleted);
        });

        Assert.Equal((5, 0, 0, false), (scrolled, session.Back, f.View.ScrollBack, f.View.IsScrolledBack));
        Assert.Equal(2, socket.Sent.Count);
        Assert.Equal(TerminalSocketMessages.Scroll(5), socket.Sent[0]);
    }

    [Fact]
    public async Task A_frame_sets_the_offset_it_was_drawn_for_and_the_cursor_shows_only_at_live()
    {
        var seen = new List<int>();
        var (_, _, session, drawn) = await Play(
            [FrameAt(5, 20), FrameAt(0, 20)],
            s => seen.Add(s.Back),
            s => seen.Add(s.Back));

        Assert.Equal([5, 0], seen);
        Assert.Equal([5, 0], drawn.Select(g => g.Back));
        Assert.Equal([false, true], drawn.Select(g => TerminalPainter.DrawsCursor(g, scrollOffset: 0)));
        Assert.Equal(0, session.Back);
    }

    // A frame for an older scroll lands after a key put the pane on live: the pane follows the frame it draws, so its history rows get no cursor.
    [Fact]
    public async Task A_late_frame_for_an_old_scroll_draws_no_cursor_and_the_next_live_frame_does()
    {
        var after = new List<int>();
        var (f, _, _, drawn) = await Play(
            [FrameAt(0, 20), FrameAt(5, 20), FrameAt(0, 20)],
            s =>
            {
                _ = s.ScrollPage(1, Token);
                _ = s.SendKey("A", "a", Token);
                after.Add(s.Back);
            },
            s => after.Add(s.Back),
            s => after.Add(s.Back));

        Assert.Equal([0, 5, 0], after);
        Assert.Equal([true, false, true], drawn.Select(g => TerminalPainter.DrawsCursor(g, scrollOffset: 0)));
        Assert.Equal(0, f.View.ScrollBack);
    }
}
