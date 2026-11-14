using System.Globalization;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Terminal;

namespace Aw.Sindri.Ui.Tests.Terminal;

/// <summary>The parser against a scrubbed real Claude Code session (A8's fixture, tests/fixtures/terminal-ws/claude-session.jsonl): every frame draws, and the known cells come out right.</summary>
[Lane(TestLane.Unit)]
public sealed class TerminalFixtureTests
{
    private static List<(int Seq, TerminalGrid Grid, int W, int H)> Frames()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "terminal-ws", "claude-session.jsonl");
        var frames = new List<(int, TerminalGrid, int, int)>();
        foreach (var line in File.ReadLines(path))
        {
            using var doc = JsonDocument.Parse(line);
            var msg = doc.RootElement.GetProperty("msg");
            if (msg.GetProperty("t").GetString() != "frame")
            {
                continue;
            }

            var rows = msg.GetProperty("rows").EnumerateArray().Select(r => r.GetString() ?? string.Empty).ToList();
            var cursor = msg.GetProperty("cursor");
            var w = msg.GetProperty("w").GetInt32();
            var h = msg.GetProperty("h").GetInt32();
            var status = new TerminalStatus(
                Width: w,
                Height: h,
                CursorX: cursor.GetProperty("x").GetInt32(),
                CursorY: cursor.GetProperty("y").GetInt32(),
                InMode: msg.GetProperty("inMode").GetInt32() != 0,
                Alternate: msg.GetProperty("alt").GetInt32() != 0,
                Dead: msg.GetProperty("dead").GetInt32() != 0,
                DeadStatus: null);
            frames.Add((msg.GetProperty("seq").GetInt32(), TerminalFrameParser.Parse(rows, status), w, h));
        }

        return frames;
    }

    [Fact]
    public void Every_frame_parses_to_the_size_and_cursor_it_carries()
    {
        var frames = Frames();

        Assert.NotEmpty(frames);
        foreach (var (seq, grid, w, h) in frames)
        {
            Assert.True((w, h, grid.History) == (grid.Width, grid.Height, 0), $"frame {seq.ToString(CultureInfo.InvariantCulture)} size");
            Assert.InRange(grid.CursorX, 0, w - 1);
            Assert.InRange(grid.CursorY, 0, h - 1);
        }
    }

    [Fact]
    public void No_escape_byte_is_left_in_any_cell()
    {
        foreach (var (seq, grid, _, _) in Frames())
        {
            for (var r = 0; r < grid.Rows; r++)
            {
                Assert.False(grid.RowText(r).Contains('\u001b', StringComparison.Ordinal), $"frame {seq.ToString(CultureInfo.InvariantCulture)} row {r.ToString(CultureInfo.InvariantCulture)}");
            }
        }
    }

    [Fact]
    public void The_first_frame_is_an_empty_screen_with_the_cursor_at_the_top_left()
    {
        var first = Frames()[0].Grid;

        Assert.Equal((0, 0), (first.CursorX, first.CursorY));
        Assert.All(Enumerable.Range(0, first.Rows), r => Assert.Equal(string.Empty, first.RowText(r)));
    }

    [Fact]
    public void The_trust_dialog_rule_is_drawn_in_colour_220()
    {
        var dialog = Frames().Single(f => f.Seq == 2).Grid;

        Assert.Equal(("─", TerminalColor.Indexed(220)), (dialog.Cell(1, 0).Text, dialog.Cell(1, 0).Fg));
    }

    [Fact]
    public void The_welcome_banner_has_its_logo_colours_and_a_bold_title_and_a_grey_version()
    {
        var banner = Frames().Single(f => f.Seq == 5).Grid;
        var title = banner.RowText(0).IndexOf("Claude", StringComparison.Ordinal);
        var version = banner.RowText(0).IndexOf("v2.1.322", StringComparison.Ordinal);

        Assert.Equal(TerminalColor.Indexed(174), banner.Cell(0, 1).Fg);
        Assert.Equal(TerminalColor.Indexed(16), banner.Cell(0, 2).Bg);
        Assert.True(title > 0);
        Assert.True(banner.Cell(0, title).Bold);
        Assert.Equal(TerminalColor.Default, banner.Cell(0, title).Fg);
        Assert.True(version > 0);
        Assert.Equal(TerminalColor.Indexed(246), banner.Cell(0, version).Fg);
        Assert.False(banner.Cell(0, version).Bold);
    }

    [Fact]
    public void A_typed_character_moves_the_cursor_one_cell_right_between_frames()
    {
        var frames = Frames();
        var a = frames.Single(f => f.Seq == 5).Grid;
        var b = frames.Single(f => f.Seq == 6).Grid;

        Assert.Equal((a.CursorX + 1, a.CursorY), (b.CursorX, b.CursorY));
    }

    [Fact]
    public void The_input_placeholder_Try_is_dim()
    {
        var grid = Frames().Single(f => f.Seq == 5).Grid;
        var found = false;
        for (var r = 0; r < grid.Rows && !found; r++)
        {
            for (var c = 0; c + 2 < grid.Width && !found; c++)
            {
                if (grid.Cell(r, c).Text == "T" && grid.Cell(r, c + 1).Text == "r" && grid.Cell(r, c + 2).Text == "y")
                {
                    found = true;
                    Assert.True(grid.Cell(r, c).Dim);
                }
            }
        }

        Assert.True(found, "no 'Try' in frame 5");
    }
}
