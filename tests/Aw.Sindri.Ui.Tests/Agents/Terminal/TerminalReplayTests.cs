using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Agents;

namespace Aw.Sindri.Ui.Tests.Agents.Terminal;

/// <summary>A8's fixture (a real Claude Code session) played into the view model as a fake source would: ready, then every frame in order.</summary>
[Lane(TestLane.Unit)]
public sealed class TerminalReplayTests
{
    [Fact]
    public void Replaying_the_fixture_attaches_and_ends_on_the_last_screen_with_the_dimming_gone()
    {
        var f = new TerminalFixture();
        f.View.Open(TerminalFixture.Lane(), TerminalOpenIntent.Look);
        f.View.SocketOpening();
        var states = new List<TerminalState>();
        var frames = 0;
        var lastRows = 0;
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "terminal-ws", "claude-session.jsonl");

        foreach (var line in File.ReadLines(path))
        {
            using var doc = JsonDocument.Parse(line);
            if (!string.Equals(doc.RootElement.GetProperty("dir").GetString(), "out", StringComparison.Ordinal))
            {
                continue;
            }

            var msg = doc.RootElement.GetProperty("msg");
            var kind = msg.GetProperty("t").GetString();
            if (string.Equals(kind, "ready", StringComparison.Ordinal))
            {
                _ = f.View.Ready();
            }
            else if (string.Equals(kind, "frame", StringComparison.Ordinal))
            {
                var rows = msg.GetProperty("rows").EnumerateArray().Select(r => r.GetString() ?? string.Empty).ToList();
                f.View.FrameReceived(rows, dead: msg.GetProperty("dead").GetInt32() != 0);
                (frames, lastRows) = (frames + 1, rows.Count);
            }

            states.Add(f.View.State);
        }

        Assert.True(frames > 1);
        Assert.Equal(lastRows, f.View.Rows.Count);
        Assert.Equal((TerminalState.Attached, false), (f.View.State, f.View.IsDimmed));
        Assert.DoesNotContain(TerminalState.Ended, states);
    }

    [Fact]
    public void A_dead_frame_after_the_fixture_freezes_the_last_screen()
    {
        var f = new TerminalFixture().Attached(TerminalFixture.OneOff());

        f.View.FrameReceived(["bye"], dead: true);

        Assert.Equal((TerminalState.Ended, "bye"), (f.View.State, f.View.Rows[0]));
    }
}
