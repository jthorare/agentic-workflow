using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Terminal;

namespace Aw.Sindri.Ui.Tests.Terminal;

/// <summary>The way C cell-grid renderer (agents page spec §15.7, #3383): capture-pane text plus the display-message line in, a grid out.</summary>
[Lane(TestLane.Unit)]
public sealed class TerminalFrameParserTests
{
    private const string Esc = "\u001b";

    // cursor_x cursor_y width height in_mode alternate_on dead dead_status
    private static string Status(int w = 10, int h = 2, int cx = 0, int cy = 0, int mode = 0, int alt = 0, int dead = 0, string deadStatus = "") =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{cx} {cy} {w} {h} {mode} {alt} {dead} {deadStatus}\n");

    private static TerminalGrid Parse(string capture, string? status = null) =>
        TerminalFrameParser.Parse(capture, status ?? Status());

    [Fact]
    public void Plain_text_lands_in_its_cells_and_the_grid_has_the_pane_size()
    {
        var grid = Parse("hi\nyo\n", Status(w: 4, h: 2));

        Assert.Equal((4, 2, 0, 2), (grid.Width, grid.Height, grid.History, grid.Rows));
        Assert.Equal("hi", grid.RowText(0));
        Assert.Equal("yo", grid.RowText(1));
        Assert.Equal("h", grid.Cell(0, 0).Text);
        Assert.Equal(TerminalColor.Default, grid.Cell(0, 0).Fg);
    }

    [Theory]
    [InlineData("31", 1)]
    [InlineData("37", 7)]
    [InlineData("91", 9)]
    [InlineData("97", 15)]
    public void A_sixteen_colour_foreground_is_a_palette_slot(string code, int slot)
    {
        var grid = Parse($"{Esc}[{code}mx{Esc}[0m\n\n");

        Assert.Equal(TerminalColor.Indexed(slot), grid.Cell(0, 0).Fg);
    }

    [Theory]
    [InlineData("42", 2)]
    [InlineData("100", 8)]
    [InlineData("107", 15)]
    public void A_sixteen_colour_background_is_a_palette_slot(string code, int slot)
    {
        var grid = Parse($"{Esc}[{code}mx\n\n");

        Assert.Equal(TerminalColor.Indexed(slot), grid.Cell(0, 0).Bg);
    }

    [Fact]
    public void A_256_colour_code_names_its_slot_in_both_planes()
    {
        var grid = Parse($"{Esc}[38;5;208;48;5;17mx\n\n");

        Assert.Equal(TerminalColor.Indexed(208), grid.Cell(0, 0).Fg);
        Assert.Equal(TerminalColor.Indexed(17), grid.Cell(0, 0).Bg);
    }

    [Theory]
    [InlineData("38;2;10;20;30")]
    [InlineData("38:2::10:20:30")]
    [InlineData("38:2:10:20:30")]
    public void A_truecolour_code_packs_red_green_blue(string code)
    {
        var grid = Parse($"{Esc}[{code}mx\n\n");

        Assert.Equal(TerminalColor.FromRgb(10, 20, 30), grid.Cell(0, 0).Fg);
    }

    [Fact]
    public void Truecolour_background_is_read()
    {
        var grid = Parse($"{Esc}[48;2;255;0;128mx\n\n");

        Assert.Equal(TerminalColor.FromRgb(255, 0, 128), grid.Cell(0, 0).Bg);
    }

    [Fact]
    public void Reset_clears_colour_and_attributes_for_the_next_cells()
    {
        var grid = Parse($"{Esc}[1;31;44ma{Esc}[0mb{Esc}[1mc{Esc}[mD\n\n");

        Assert.True(grid.Cell(0, 0).Bold);
        Assert.Equal(TerminalCell.Blank with { Text = "b" }, grid.Cell(0, 1));
        Assert.True(grid.Cell(0, 2).Bold);
        Assert.Equal(TerminalCell.Blank with { Text = "D" }, grid.Cell(0, 3));
    }

    [Fact]
    public void Default_colour_codes_return_one_plane_only()
    {
        var grid = Parse($"{Esc}[31;42ma{Esc}[39mb{Esc}[49mc\n\n");

        Assert.Equal((TerminalColor.Indexed(1), TerminalColor.Indexed(2)), (grid.Cell(0, 0).Fg, grid.Cell(0, 0).Bg));
        Assert.Equal((TerminalColor.Default, TerminalColor.Indexed(2)), (grid.Cell(0, 1).Fg, grid.Cell(0, 1).Bg));
        Assert.Equal((TerminalColor.Default, TerminalColor.Default), (grid.Cell(0, 2).Fg, grid.Cell(0, 2).Bg));
    }

    [Fact]
    public void Bold_italic_underline_and_inverse_set_and_clear_on_their_own_codes()
    {
        var grid = Parse($"{Esc}[1;3;4;7ma{Esc}[22mb{Esc}[23mc{Esc}[24md{Esc}[27me\n\n", Status(w: 5));

        Assert.Equal(TerminalStyle.Bold | TerminalStyle.Italic | TerminalStyle.Underline | TerminalStyle.Inverse, grid.Cell(0, 0).Style);
        Assert.Equal(TerminalStyle.Italic | TerminalStyle.Underline | TerminalStyle.Inverse, grid.Cell(0, 1).Style);
        Assert.Equal(TerminalStyle.Underline | TerminalStyle.Inverse, grid.Cell(0, 2).Style);
        Assert.Equal(TerminalStyle.Inverse, grid.Cell(0, 3).Style);
        Assert.Equal(TerminalStyle.None, grid.Cell(0, 4).Style);
        Assert.True(grid.Cell(0, 0).Inverse);
    }

    [Fact]
    public void A_colour_left_open_at_a_row_end_carries_into_the_next_row_as_capture_pane_writes_it()
    {
        var grid = Parse($"{Esc}[41mab\ncd\n", Status(w: 4));

        Assert.Equal(TerminalColor.Indexed(1), grid.Cell(1, 0).Bg);
        Assert.Equal(TerminalColor.Indexed(1), grid.Cell(1, 1).Bg);
    }

    [Fact]
    public void A_reset_on_a_row_stops_the_carry_into_the_next_row()
    {
        var grid = Parse($"{Esc}[41mab{Esc}[0m\ncd\n", Status(w: 4));

        Assert.Equal(TerminalCell.Blank with { Text = "c" }, grid.Cell(1, 0));
    }

    [Fact]
    public void Dim_sets_on_SGR_2_and_clears_with_22_together_with_bold()
    {
        var grid = Parse($"{Esc}[2ma{Esc}[1mb{Esc}[22mc\n\n", Status(w: 4));

        Assert.True(grid.Cell(0, 0).Dim);
        Assert.Equal(TerminalStyle.Bold | TerminalStyle.Dim, grid.Cell(0, 1).Style);
        Assert.Equal(TerminalStyle.None, grid.Cell(0, 2).Style);
    }

    [Fact]
    public void Escape_sequences_that_are_not_colours_draw_nothing()
    {
        var grid = Parse($"a{Esc}[2Kb{Esc}]0;title\u0007c{Esc}]8;;http://x{Esc}\\d\n\n");

        Assert.Equal("abcd", grid.RowText(0));
    }

    [Fact]
    public void A_wide_character_takes_two_cells_and_the_next_character_lands_after_them()
    {
        var grid = Parse("a漢b\n\n", Status(w: 6));

        Assert.Equal(("漢", 2), (grid.Cell(0, 1).Text, grid.Cell(0, 1).Columns));
        Assert.Equal(("", 0), (grid.Cell(0, 2).Text, grid.Cell(0, 2).Columns));
        Assert.Equal("b", grid.Cell(0, 3).Text);
        Assert.Equal("a漢b", grid.RowText(0));
    }

    [Fact]
    public void A_wide_character_that_does_not_fit_the_last_column_is_left_out()
    {
        var grid = Parse("ab漢\n\n", Status(w: 3));

        Assert.Equal("ab", grid.RowText(0));
        Assert.Equal(TerminalCell.Blank, grid.Cell(0, 2));
    }

    [Fact]
    public void A_combining_mark_joins_the_cell_before_it()
    {
        var grid = Parse("éx\n\n");

        Assert.Equal(("é", 1), (grid.Cell(0, 0).Text, grid.Cell(0, 0).Columns));
        Assert.Equal("x", grid.Cell(0, 1).Text);
    }

    [Fact]
    public void A_row_shorter_than_the_width_is_padded_with_blank_default_cells()
    {
        var grid = Parse("ab\n\n", Status(w: 5));

        for (var c = 2; c < 5; c++)
        {
            Assert.Equal(TerminalCell.Blank, grid.Cell(0, c));
        }

        Assert.Equal(TerminalCell.Blank, grid.Cell(1, 0));
    }

    [Fact]
    public void A_capture_with_fewer_lines_than_the_height_pads_blank_rows_at_the_bottom()
    {
        var grid = Parse("only\n", Status(w: 4, h: 3));

        Assert.Equal((3, 0), (grid.Rows, grid.History));
        Assert.Equal("only", grid.RowText(0));
        Assert.Equal("", grid.RowText(2));
    }

    [Fact]
    public void The_cursor_comes_from_the_display_message_line()
    {
        var grid = Parse("ab\ncd\n", Status(w: 5, h: 2, cx: 3, cy: 1));

        Assert.Equal((3, 1), (grid.CursorX, grid.CursorY));
    }

    [Fact]
    public void The_alternate_screen_and_mode_flags_are_read()
    {
        var normal = Parse("a\n\n", Status(alt: 0, mode: 0));
        var full = Parse("a\n\n", Status(alt: 1, mode: 1));

        Assert.Equal((false, false), (normal.AlternateScreen, normal.InMode));
        Assert.Equal((true, true), (full.AlternateScreen, full.InMode));
    }

    [Fact]
    public void A_dead_pane_carries_its_exit_status()
    {
        var alive = Parse("a\n\n", Status(dead: 0));
        var dead = Parse("a\n\n", Status(dead: 1, deadStatus: "137"));
        var deadNoStatus = Parse("a\n\n", Status(dead: 1));

        Assert.False(alive.Dead);
        Assert.Null(alive.DeadStatus);
        Assert.True(dead.Dead);
        Assert.Equal(137, dead.DeadStatus);
        Assert.True(deadNoStatus.Dead);
        Assert.Null(deadNoStatus.DeadStatus);
    }

    [Fact]
    public void A_capture_taller_than_the_pane_keeps_the_extra_leading_lines_as_scrollback()
    {
        var grid = Parse("old1\nold2\nlive1\nlive2\n", Status(w: 5, h: 2, cx: 1, cy: 1));

        Assert.Equal((4, 2, 2), (grid.Rows, grid.Height, grid.History));
        Assert.Equal("old1", grid.RowText(0));
        Assert.Equal("old2", grid.RowText(1));
        Assert.Equal("live1", grid.RowText(2));
        Assert.Equal((1, 1), (grid.CursorX, grid.CursorY));
    }

    [Fact]
    public void A_resize_that_narrows_the_pane_cuts_the_rows_to_the_new_width()
    {
        var wide = Parse("abcdef\nuvwxyz\n", Status(w: 6, h: 2));
        var narrow = Parse("abcd\nuvwx\n", Status(w: 4, h: 2));

        Assert.Equal(6, wide.Width);
        Assert.Equal(4, narrow.Width);
        Assert.Equal("abcd", narrow.RowText(0));
    }

    [Fact]
    public void Resize_cuts_and_pads_the_grid_keeping_the_bottom_rows()
    {
        var grid = Parse("abcdef\nuvwxyz\n", Status(w: 6, h: 2, cx: 5, cy: 1));

        var smaller = grid.Resize(3, 1);
        var bigger = grid.Resize(8, 3);

        Assert.Equal((3, 1), (smaller.Width, smaller.Height));
        Assert.Equal("uvw", smaller.RowText(smaller.History));
        Assert.Equal((8, 3), (bigger.Width, bigger.Height));
        Assert.Equal("uvwxyz", bigger.RowText(bigger.Rows - 1));
        Assert.Equal(TerminalCell.Blank, bigger.Cell(0, 7));
    }

    [Fact]
    public void A_status_line_that_does_not_parse_still_draws_from_the_capture()
    {
        var grid = TerminalFrameParser.Parse("hello\nhi\n", "can't find pane");

        Assert.Equal((5, 2), (grid.Width, grid.Height));
        Assert.Equal((0, 0), (grid.CursorX, grid.CursorY));
        Assert.Equal("hello", grid.RowText(0));
    }

    [Fact]
    public void Windows_line_endings_and_a_missing_final_newline_are_read_alike()
    {
        var a = Parse("ab\r\ncd", Status(w: 3));
        var b = Parse("ab\ncd\n", Status(w: 3));

        Assert.Equal((b.RowText(0), b.RowText(1)), (a.RowText(0), a.RowText(1)));
    }

    [Fact]
    public void A_huge_or_corrupt_size_is_clamped_to_the_socket_limits()
    {
        var grid = TerminalFrameParser.Parse("ab\n", "0 0 2000000000 2000000000 0 0 0 \n");

        Assert.Equal((TerminalStatus.MaxColumns, TerminalStatus.MaxRows), (grid.Width, grid.Height));
    }

    [Fact]
    public void An_escape_with_an_intermediate_byte_skips_all_three_bytes()
    {
        var grid = Parse($"a{Esc}(Bb\n\n");

        Assert.Equal("ab", grid.RowText(0));
    }

    [Fact]
    public void A_private_prefix_sequence_ending_in_m_is_not_a_colour()
    {
        var grid = Parse($"{Esc}[?31mx{Esc}[>1mydz\n\n");

        Assert.Equal("xydz", grid.RowText(0));
        Assert.Equal(TerminalColor.Default, grid.Cell(0, 0).Fg);
        Assert.False(grid.Cell(0, 1).Bold);
    }
}
