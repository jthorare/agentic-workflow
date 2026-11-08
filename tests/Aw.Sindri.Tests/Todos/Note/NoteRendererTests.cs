using System.Reflection;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Todos.Note;
using static Aw.Sindri.Tests.Todos.Note.NoteSamples;

namespace Aw.Sindri.Tests.Todos.Note;

/// <summary>The note renderer (todo spec §4.5): the line for a todo, the escapes, and a rebuild that touches only the lines that need it.</summary>
[Lane(TestLane.Unit)]
public sealed class NoteRendererTests
{
    private static readonly Assembly _tests = typeof(NoteRendererTests).Assembly;

    [Fact]
    public void Every_line_shape_renders_as_section_4_5_shows()
    {
        var todos = new[]
        {
            S(1, "reply to Sam about the offsite", "2026-11-14"),
            S(2, "rotate the staging key", "2026-11-10") with { Links = ["sindri://reviews/jthorare/agentic-workflow/12", "sindri://linear/ENG-12"] },
            S(3, "call the dentist"),
            S(4, "renew the cert") with { Done = true, DoneOn = D("2026-11-07") },
            S(5, "ship it", "2026-11-09") with { Done = true, Links = ["sindri://item/ITM-0042"] },
            S(6, "all fields", "2026-11-09") with { Done = true, DoneOn = D("2026-11-08"), Links = ["sindri://linear/ENG-12"] },
            S(7, "a`b <tag> [[x]]"),
        };

        var text = string.Concat(todos.Select(static t => NoteRenderer.RenderLine(t) + "\n"));

        Golden.Match(text, _tests, "Todos/Note/Golden/render-lines.txt", Normalization.None);
    }

    [Theory]
    [InlineData("a`b", "a\\`b")]
    [InlineData("a<b", "a\\<b")]
    [InlineData("see ![[x]]", "see \\![[x]]")]
    [InlineData("see [[x]]", "see \\[[x]]")]
    [InlineData("50%% off", "50\\%% off")]
    [InlineData("$=dv", "\\$=dv")]
    [InlineData("[[[a", "\\[[[a")]
    [InlineData("a\\b", "a\\b")]
    [InlineData("\\<", "\\\\<")]
    [InlineData("plain words", "plain words")]
    public void Escape_puts_a_backslash_before_each_running_or_embedding_sequence(string plain, string escaped)
    {
        Assert.Equal(escaped, NoteRenderer.Escape(plain));
        Assert.Equal(plain, NoteRenderer.Unescape(escaped));
    }

    [Fact]
    public void Unescape_leaves_a_backslash_that_does_not_guard_a_sequence() =>
        Assert.Equal("C:\\temp \\n", NoteRenderer.Unescape("C:\\temp \\n"));

    [Fact]
    public void Rebuild_changes_only_the_named_lines_and_adds_new_todos_after_the_last_todo_line()
    {
        var note = Lines(
            "# Todo",
            string.Empty,
            "Intro paragraph.  ",
            L(1, "keep me"),
            L(2, "change me"),
            "```",
            "- [ ] not a todo",
            "```",
            L(3, "remove me"),
            "Footer");

        var rebuilt = NoteRenderer.Rebuild(
            NoteParser.Parse(note),
            [
                NoteLineEdit.Replace(5, L(2, "changed")),
                NoteLineEdit.Remove(9),
                NoteLineEdit.Append(L(4, "added one")),
                NoteLineEdit.Append(L(5, "added two")),
            ]);

        Golden.Match(rebuilt, _tests, "Todos/Note/Golden/rebuild-mixed.txt", Normalization.None);
    }

    [Fact]
    public void Rebuild_with_no_edits_gives_the_note_back()
    {
        var note = "# Todo\r\n\r\n- [ ] a ^td-01jc5a8xq0n4w7ms2t9r3v0001  \r\nlast";

        Assert.Equal(note, NoteRenderer.Rebuild(NoteParser.Parse(note), []));
    }

    [Fact]
    public void Rebuild_adds_at_the_end_when_the_note_has_no_todo_line()
    {
        var rebuilt = NoteRenderer.Rebuild(NoteParser.Parse("# Notes\nprose\n"), [NoteLineEdit.Append("- [ ] new")]);

        Assert.Equal("# Notes\nprose\n- [ ] new\n", rebuilt);
    }

    [Fact]
    public void Rebuild_gives_a_last_line_without_a_line_ending_one_before_it_appends()
    {
        var rebuilt = NoteRenderer.Rebuild(NoteParser.Parse("# Notes\nprose"), [NoteLineEdit.Append("- [ ] new")]);

        Assert.Equal("# Notes\nprose\n- [ ] new\n", rebuilt);
    }

    [Fact]
    public void Rebuild_of_an_empty_note_writes_the_Todo_heading_first()
    {
        var rebuilt = NoteRenderer.Rebuild(NoteParser.Parse(string.Empty), [NoteLineEdit.Append("- [ ] a"), NoteLineEdit.Append("- [ ] b")]);

        Assert.Equal("# Todo\n\n- [ ] a\n- [ ] b\n", rebuilt);
    }

    [Fact]
    public void Rebuild_uses_the_line_ending_the_note_uses()
    {
        var rebuilt = NoteRenderer.Rebuild(NoteParser.Parse("# Todo\r\n- [ ] a\r\n"), [NoteLineEdit.Append("- [ ] b"), NoteLineEdit.Replace(2, "- [ ] a2")]);

        Assert.Equal("# Todo\r\n- [ ] a2\r\n- [ ] b\r\n", rebuilt);
    }

    [Fact]
    public void Replace_keeps_the_line_ending_of_the_line_it_replaces_even_when_there_is_none()
    {
        var rebuilt = NoteRenderer.Rebuild(NoteParser.Parse("x\n- [ ] a"), [NoteLineEdit.Replace(2, "- [ ] b")]);

        Assert.Equal("x\n- [ ] b", rebuilt);
    }

    [Fact]
    public void Remove_takes_the_line_and_its_ending()
    {
        var rebuilt = NoteRenderer.Rebuild(NoteParser.Parse("a\n- [ ] b\nc\n"), [NoteLineEdit.Remove(2)]);

        Assert.Equal("a\nc\n", rebuilt);
    }
}
