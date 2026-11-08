using System.Reflection;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Todos.Note;
using static Aw.Sindri.Tests.Todos.Note.NoteSamples;

namespace Aw.Sindri.Tests.Todos.Note;

/// <summary>The note parser (todo spec §4.5 line shapes and D3): goldens dump what each line becomes.</summary>
[Lane(TestLane.Unit)]
public sealed class NoteParserTests
{
    private static readonly Assembly _tests = typeof(NoteParserTests).Assembly;

    [Fact]
    public void The_four_example_lines_of_section_4_5_parse()
    {
        var note = Lines(
            "# Todo",
            string.Empty,
            "- [ ] reply to Sam about the offsite 📅 2026-11-14 ^td-01jc5a8xq0n4w7ms2t9r3vbk6e",
            "- [ ] rotate the staging key 📅 2026-11-10 [PR #12](sindri://reviews/jthorare/agentic-workflow/12) [ENG-12](sindri://linear/ENG-12) ^td-01jc5b2kz8m3r6tq1w9xh4dn7y",
            "- [ ] call the dentist ^td-01jc5c90a2b7k4m8q1r6t3w9xz",
            "- [x] renew the cert ✅ 2026-11-07 ^td-01jc5917f3h8n2p5s9v4x1z6bq");

        Golden.Match(Dump(NoteParser.Parse(note)), _tests, "Todos/Note/Golden/parse-examples.txt", Normalization.None);
    }

    [Fact]
    public void Task_lines_in_a_fenced_block_are_not_todos()
    {
        var note = Lines(
            "# Code",
            "```",
            $"- [ ] inside a fence {Block(1)}",
            "```",
            "~~~md",
            "- [ ] inside a tilde fence",
            "~~~",
            "- [ ] after the fences");

        Golden.Match(Dump(NoteParser.Parse(note)), _tests, "Todos/Note/Golden/parse-fenced.txt", Normalization.None);
    }

    [Fact]
    public void Task_lines_in_a_quote_or_callout_are_not_todos()
    {
        var note = Lines(
            $"> - [ ] in a quote {Block(2)}",
            "> [!note] a callout",
            "> - [ ] in a callout",
            "- [ ] real one");

        Golden.Match(Dump(NoteParser.Parse(note)), _tests, "Todos/Note/Golden/parse-quote.txt", Normalization.None);
    }

    [Fact]
    public void Indented_task_lines_are_not_todos()
    {
        var note = Lines(
            "- parent",
            $"  - [ ] child task {Block(3)}",
            "\t- [ ] tab indented",
            " - [ ] one space indented",
            "- [ ] top level");

        Golden.Match(Dump(NoteParser.Parse(note)), _tests, "Todos/Note/Golden/parse-indented.txt", Normalization.None);
    }

    [Fact]
    public void An_id_that_is_not_26_lowercase_ulid_characters_is_not_an_id_and_the_line_is_new()
    {
        var note = Lines(
            "- [ ] short id ^td-01jc5a8xq0n4w7ms2t9r3vbk6",
            "- [ ] upper id ^td-01JC5A8XQ0N4W7MS2T9R3VBK6E");

        Golden.Match(Dump(NoteParser.Parse(note)), _tests, "Todos/Note/Golden/parse-bad-id.txt", Normalization.None);
    }

    [Fact]
    public void Each_line_that_breaks_D3_says_how()
    {
        var note = Lines(
            $"- [ ] two ids ^td-{Ulid(1)} {Block(2)}",
            $"- [ ] two dues 📅 2026-11-10 📅 2026-11-11 {Block(3)}",
            $"- [ ] marker inside 📅 text here {Block(4)}",
            $"- [ ] pasted [docs](https://example.com/a) link {Block(5)}",
            $"- [ ] trailing [docs](https://example.com) {Block(6)}",
            $"- [ ] bad char‮ {Block(7)}",
            $"- [ ] {Block(8)}");

        Golden.Match(Dump(NoteParser.Parse(note)), _tests, "Todos/Note/Golden/parse-d3-breaks.txt", Normalization.None);
    }

    [Theory]
    [InlineData("hi\U000E0041")]
    [InlineData("hi\uD800")]
    [InlineData("hi\uDC00x")]
    [InlineData("hi\u200B")]
    public void A_tag_character_or_a_lone_surrogate_in_the_text_breaks_D3(string text)
    {
        var todo = NoteParser.Parse($"- [ ] {text} {Block(1)}\n").Todos.Single();

        Assert.Equal(NoteBreak.ForbiddenCharacter, todo.Break);
    }

    [Fact]
    public void A_text_the_link_check_cannot_judge_in_time_is_a_break_line_and_never_a_throw()
    {
        var line = "- [ ] " + string.Concat(Enumerable.Repeat("[a](", 5000)) + $" {Block(1)}\n";

        var thrown = Record.Exception(() => NoteParser.Parse(line));

        Assert.Null(thrown);
    }

    [Fact]
    public void A_sindri_link_of_a_kind_that_does_not_ship_is_a_pasted_link()
    {
        var todo = NoteParser.Parse($"- [ ] a [run](sindri://workflow/a-b) {Block(1)}\n").Todos.Single();

        Assert.Equal(NoteBreak.ForeignLink, todo.Break);
    }

    [Theory]
    [InlineData("- [ ] a ^abc123", NoteBreak.ForeignBlockId)]
    [InlineData("- [ ] a ^abc123 ^td-01jc5a8xq0n4w7ms2t9r3vbk6e", NoteBreak.ForeignBlockId)]
    [InlineData("- [ ] a^abc123", NoteBreak.None)]
    [InlineData("- [ ] a ^abc123 b", NoteBreak.None)]
    public void A_block_id_of_the_owners_own_breaks_the_line_only_at_its_end(string line, NoteBreak expected) =>
        Assert.Equal(expected, NoteParser.Parse(line + "\n").Todos.Single().Break);

    [Theory]
    [InlineData(280, NoteBreak.None)]
    [InlineData(281, NoteBreak.TextTooLong)]
    public void Text_is_one_to_280_characters(int length, NoteBreak expected)
    {
        var todo = NoteParser.Parse($"- [ ] {new string('a', length)} {Block(1)}\n").Todos.Single();

        Assert.Equal(expected, todo.Break);
    }

    [Fact]
    public void A_final_line_ending_adds_no_empty_line()
    {
        Assert.Equal(2, NoteParser.Parse("a\nb\n").Lines.Count);
        Assert.Equal(2, NoteParser.Parse("a\nb").Lines.Count);
        Assert.Empty(NoteParser.Parse(string.Empty).Lines);
        _ = Assert.Single(NoteParser.Parse("\n").Lines);
    }

    [Fact]
    public void Each_line_keeps_its_own_line_ending()
    {
        var eols = NoteParser.Parse("a\nb\r\nc").Lines.Select(static l => l.Eol);

        Same(["\n", "\r\n", string.Empty], eols);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\n")]
    [InlineData("# Todo\n\nSome prose.  \nMore prose\n")]
    [InlineData("heading\r\n\r\n- [ ] a\r\n  indented  \r\nlast line no ending")]
    [InlineData("tabs\tand   spaces   \n\n\n- [ ] x ^td-01jc5a8xq0n4w7ms2t9r3vbk6e\n")]
    [InlineData("ünïcödé 📅 text\n- [x] done ✅ 2026-11-07\n")]
    public void Render_gives_back_the_note_byte_for_byte(string note) =>
        Assert.Equal(note, NoteParser.Parse(note).Render());

    [Fact]
    public void A_todo_line_has_its_number_and_its_raw_text_without_the_line_ending()
    {
        var document = NoteParser.Parse("a\r\n- [ ] b  ^td-01jc5a8xq0n4w7ms2t9r3vbk6e\r\n");

        var todo = document.Todos.Single();

        Assert.Equal(2, todo.Number);
        Assert.Equal("- [ ] b  ^td-01jc5a8xq0n4w7ms2t9r3vbk6e", todo.Raw);
        Assert.Equal("b", todo.Text);
    }

    [Theory]
    [InlineData("- [X] shouted", true)]
    [InlineData("- [x] quiet", true)]
    [InlineData("- [ ] open", false)]
    public void Both_cases_of_x_are_done(string line, bool done) =>
        Assert.Equal(done, NoteParser.Parse(line + "\n").Todos.Single().Done);

    [Theory]
    [InlineData("- [/] in progress")]
    [InlineData("- [-] cancelled")]
    [InlineData("* [ ] star bullet")]
    [InlineData("+ [ ] plus bullet")]
    [InlineData("1. [ ] numbered")]
    [InlineData("-[ ] no space")]
    public void Only_dash_space_open_or_x_task_lines_are_todos(string line) =>
        Assert.Empty(NoteParser.Parse(line + "\n").Todos);

    [Fact]
    public void A_done_line_without_a_date_has_no_done_date()
    {
        var todo = NoteParser.Parse($"- [x] a {Block(1)}\n").Todos.Single();

        Assert.True(todo.Done);
        Assert.Null(todo.DoneOn);
    }

    [Fact]
    public void Escapes_in_the_text_are_undone_on_read()
    {
        var todo = NoteParser.Parse($"- [ ] see \\[[x]] and \\`code\\` {Block(1)}\n").Todos.Single();

        Assert.Equal("see [[x]] and `code`", todo.Text);
        Assert.Equal(NoteBreak.None, todo.Break);
    }

    [Fact]
    public void A_line_adopted_from_the_owner_keeps_its_raw_wikilink_text()
    {
        var todo = NoteParser.Parse("- [ ] read [[a link]] today\n").Todos.Single();

        Assert.Equal("read [[a link]] today", todo.Text);
        Assert.Null(todo.Id);
        Assert.Equal(NoteBreak.None, todo.Break);
    }

    [Theory]
    [InlineData(NoteBreak.TwoIds, "it has two ids")]
    [InlineData(NoteBreak.TwoDueMarkers, "it has two due dates")]
    [InlineData(NoteBreak.MarkerInText, "it has a 📅, ✅ or ^td- marker inside the text")]
    [InlineData(NoteBreak.ForeignLink, "it has a markdown link")]
    [InlineData(NoteBreak.ForbiddenCharacter, "it has a character Sindri can't store")]
    [InlineData(NoteBreak.EmptyText, "it has no text")]
    [InlineData(NoteBreak.TextTooLong, "it is longer than 280 characters")]
    [InlineData(NoteBreak.ForeignBlockId, "it ends in a block id (^…) of its own")]
    public void Each_break_has_its_words_for_the_sync_line(NoteBreak reason, string words) =>
        Assert.Equal(words, NoteBreakReasons.Of(reason));

    [Fact]
    public void None_has_no_words() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => NoteBreakReasons.Of(NoteBreak.None));
}
