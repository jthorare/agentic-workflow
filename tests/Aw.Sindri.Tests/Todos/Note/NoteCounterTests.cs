using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Todos.Note;

namespace Aw.Sindri.Tests.Todos.Note;

/// <summary>What Check note counts (todo spec §3.4, §4.4): open, done, other lines, the lines that can't import (up to five), and how many fit under the 500 open todos limit.</summary>
[Lane(TestLane.Unit)]
public sealed class NoteCounterTests
{
    [Fact]
    public void Open_done_and_other_lines_are_counted_and_a_line_with_a_pasted_link_cannot_import()
    {
        var counts = NoteCounter.Count(NoteParser.Parse("# Todo\n- [ ] reply to Sam\n- [ ] read [docs](https://example.com)\n- [x] pay rent\n\nnotes below\n"));

        Assert.Equal((2, 1, 3, 1, 1), (counts.Open, counts.Done, counts.Other, counts.Importable, counts.CantImport.Count));
        Assert.Equal([3], counts.CantImport.Select(static c => c.Line));
        Assert.Equal("it has a markdown link", counts.CantImport[0].Reason);
    }

    [Fact]
    public void An_empty_note_has_nothing_in_it()
    {
        var counts = NoteCounter.Count(NoteParser.Parse(string.Empty));

        Assert.Equal((0, 0, 0, 0, 0), (counts.Open, counts.Done, counts.Other, counts.Importable, counts.CantImport.Count));
    }

    [Fact]
    public void Task_lines_in_a_fence_a_quote_or_an_indent_and_other_check_states_are_other_lines()
    {
        var counts = NoteCounter.Count(NoteParser.Parse("```\n- [ ] in a fence\n```\n> - [ ] quoted\n  - [ ] indented\n- [/] half done\n- [ ] real\n"));

        Assert.Equal((1, 0, 6, 1), (counts.Open, counts.Done, counts.Other, counts.Importable));
    }

    [Fact]
    public void A_done_line_that_breaks_the_text_rules_is_not_counted_as_one_that_cannot_import()
    {
        var counts = NoteCounter.Count(NoteParser.Parse("- [x] see [docs](https://example.com)\n- [ ] fine\n"));

        Assert.Equal((1, 1, 0, 1), (counts.Open, counts.Done, counts.CantImport.Count, counts.Importable));
    }

    [Fact]
    public void Every_line_that_cannot_import_is_listed_with_its_number_in_file_order()
    {
        var note = string.Concat(Enumerable.Range(1, 7).Select(static i => string.Create(CultureInfo.InvariantCulture, $"- [ ] item {i} [x](https://e.com/{i})\n")));

        var counts = NoteCounter.Count(NoteParser.Parse(note));

        Assert.Equal([1, 2, 3, 4, 5, 6, 7], counts.CantImport.Select(static c => c.Line));
    }

    [Theory]
    [InlineData(600, 42, 458)]
    [InlineData(3, 0, 3)]
    [InlineData(10, 495, 5)]
    [InlineData(10, 500, 0)]
    [InlineData(10, 520, 0)]
    [InlineData(0, 10, 0)]
    public void The_import_takes_what_fits_under_500_open_todos_beside_the_ones_Sindri_has(int importable, int sindriOpen, int expected) =>
        Assert.Equal(expected, NoteCounter.WillImport(importable, sindriOpen));
}
