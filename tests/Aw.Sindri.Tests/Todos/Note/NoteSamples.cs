using System.Globalization;
using System.Text;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Core.Todos.Note;

namespace Aw.Sindri.Tests.Todos.Note;

/// <summary>Builders and one-line renderings shared by the note tests: todo number n is the id 'td_01jc5a8xq0n4w7ms2t9r3v' plus n as four digits, so a failing assertion names the todo by its number.</summary>
internal static class NoteSamples
{
    private const string UlidStem = "01jc5a8xq0n4w7ms2t9r3v";

    public static string Ulid(int n) => UlidStem + n.ToString("0000", CultureInfo.InvariantCulture);

    public static TodoId Id(int n) => TodoId.From("td_" + Ulid(n));

    public static string Block(int n) => "^td-" + Ulid(n);

    public static DateOnly D(string iso) => DateOnly.ParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>The lines joined with "\n" and a final "\n".</summary>
    public static string Lines(params string[] lines) => string.Join('\n', lines) + "\n";

    /// <summary>An open todo line as the note holds it.</summary>
    public static string L(int n, string text, string? due = null) =>
        $"- [ ] {text}{(due is null ? string.Empty : " 📅 " + due)} {Block(n)}";

    /// <summary>A done todo line as the note holds it.</summary>
    public static string X(int n, string text, string? doneOn = null) =>
        $"- [x] {text}{(doneOn is null ? string.Empty : " ✅ " + doneOn)} {Block(n)}";

    /// <summary>Sindri's side of an open todo.</summary>
    public static SindriTodo S(int n, string text, string? due = null) =>
        new(Id(n), text, Due: due is null ? null : D(due), Links: []);

    public static NoteDocument Doc(params string[] lines) => NoteParser.Parse(Lines(lines));

    public static NoteBase Base(params string[] lines) => new(lines);

    /// <summary>Options whose minter hands out todo numbers 9000, 9001, … in order.</summary>
    public static NoteMergeOptions Options(Func<NoteMergeOptions, NoteMergeOptions>? with = null, ITextScrubber? scrubber = null)
    {
        var next = 9000;
        var options = new NoteMergeOptions(new PassIdMinter(() => Id(next++)), scrubber ?? new KeepText());
        return with is null ? options : with(options);
    }

    public static string Show(NoteIntent intent) => intent switch
    {
        NoteAdd a => $"add {N(a.Id)} \"{a.Text}\"{(a.Done ? " done" : string.Empty)}",
        NoteAdopt a => $"adopt {N(a.Id)} \"{a.Text}\" line {Num(a.LineNumber)}",
        NoteChange c => $"change {N(c.Id)}{(c.Text is null ? string.Empty : $" text=\"{c.Text}\"")}"
            + (c.DueChanged ? $" due={(c.Due is { } d ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "none")}" : string.Empty)
            + (c.Links is null ? string.Empty : $" links=[{string.Join(',', c.Links)}]"),
        NoteDone d => $"done {N(d.Id)}{(d.On is { } on ? " on=" + on.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : string.Empty)}",
        NoteReopen r => $"reopen {N(r.Id)}",
        NoteRemove r => $"remove {N(r.Id)}",
        NoteRestore r => $"restore {N(r.Id)}",
        NoteConflict c => $"conflict {N(c.Id)} sindri={ShowSide(c.Sindri)} note={ShowSide(c.Obsidian)}",
        NoteLineBlocked b => $"blocked {N(b.Id)} line {Num(b.LineNumber)}: {b.Reason}",
        _ => throw new ArgumentOutOfRangeException(nameof(intent)),
    };

    public static string Show(NoteLineEdit edit) => edit.Kind switch
    {
        NoteLineEditKind.Replace => $"replace {Num(edit.Number)}: {edit.Line}",
        NoteLineEditKind.Remove => $"remove {Num(edit.Number)}",
        NoteLineEditKind.Append => $"append: {edit.Line}",
        _ => throw new ArgumentOutOfRangeException(nameof(edit)),
    };

    /// <summary>Ordinal comparison of two line lists.</summary>
    public static void Same(IEnumerable<string> expected, IEnumerable<string> actual) => Assert.Equal(expected, actual, StringComparer.Ordinal);

    public static string Txt(string prefix, int number) => prefix + number.ToString(CultureInfo.InvariantCulture);

    /// <summary>A held removal of todo n.</summary>
    public static HeldChange Gone(int n) => new(Id(n), HeldKind.Removed, NoteLine: null);

    public static NoteSide Side(string text) => new(text, Due: null, Links: [], Done: false, Removed: false);

    public static IEnumerable<string> Intents(NoteMergeResult r) => r.Intents.Select(Show);

    public static IEnumerable<string> Edits(NoteMergeResult r) => r.LineEdits.Select(Show);

    private static string ShowSide(NoteSide s) => s.Removed ? "removed" : $"\"{s.Text}\"";

    private static string Num(int number) => number.ToString(CultureInfo.InvariantCulture);

    private static string N(TodoId id) => int.Parse(id.Value[^4..], CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);

    /// <summary>The golden form of a parsed note: one line per note line, controls and format characters as \uXXXX.</summary>
    public static string Dump(NoteDocument document)
    {
        var sb = new StringBuilder();
        foreach (var line in document.Lines)
        {
            _ = sb.Append(line.Number.ToString("00", CultureInfo.InvariantCulture))
                .Append(line.Todo is { } todo ? DumpTodo(todo) : DumpText(line.Text))
                .Append('\n');
        }

        return sb.ToString();
    }

    private static string DumpText(string text) => " line  |" + (text.Length > 0 ? " " + Visible(text) : string.Empty);

    private static string DumpDate(DateOnly? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "-";

    private static string DumpTodo(NoteTodoLine t) =>
        " todo  | id=" + (t.Id is { } id ? id.Value : "-")
        + " done=" + (t.Done ? "yes" : "no")
        + " on=" + DumpDate(t.DoneOn)
        + " due=" + DumpDate(t.Due)
        + " links=" + (t.Links.Count == 0 ? "-" : string.Join(',', t.Links))
        + " bad=" + (t.RejectedBlock ?? "-")
        + " break=" + t.Break
        + " text=" + Visible(t.Text);

    private static string Visible(string text)
    {
        var sb = new StringBuilder();
        foreach (var c in text)
        {
            _ = char.GetUnicodeCategory(c) is UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator
                ? sb.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture))
                : sb.Append(c);
        }

        return sb.ToString();
    }

    /// <summary>Passes text through unchanged.</summary>
    private sealed class KeepText : ITextScrubber
    {
        public string Scrub(string text) => text;
    }
}
