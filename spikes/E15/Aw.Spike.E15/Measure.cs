using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;

namespace Aw.Spike.E15;

/// <summary>AW_SPIKE_E15=1: run the E15 measurements in the open window (needs a display; xvfb is fine), write measure.json, exit.</summary>
internal static class Measure
{
    private const string ChipLine = "Done [[issue 123]] and [[PR 9]] end";

    public static async Task RunAndExitAsync(MainWindow window, IClassicDesktopStyleApplicationLifetime lifetime)
    {
        var code = 0;
        var outDir = Environment.GetEnvironmentVariable("AW_SPIKE_E15_OUT") ?? Path.Combine(AppContext.BaseDirectory, "measure-out");
        Directory.CreateDirectory(outDir);
        try
        {
            await Settle();
            using var stream = new MemoryStream();
            using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                w.WriteStartObject();
                await Paste(window, w);
                await Caret(window, w);
                await Bytes(window, w, outDir);
                w.WriteEndObject();
            }
            await File.WriteAllBytesAsync(Path.Combine(outDir, "measure.json"), stream.ToArray());
        }
        catch (Exception ex)
        {
            code = 1;
            await File.WriteAllTextAsync(Path.Combine(outDir, "measure-error.txt"), ex.ToString());
        }
        lifetime.Shutdown(code);
    }

    private static async Task Settle()
    {
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Render);
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background);
    }

    private static async Task Paste(MainWindow window, Utf8JsonWriter w)
    {
        var editor = window.Editor;
        var text = Fixtures.BigText(10_000);
        w.WriteStartObject("paste");
        w.WriteNumber("lines", 10_000);
        w.WriteNumber("chars", text.Length);
        foreach (var (name, on, viaInput) in new[] { ("insert_colorizer_on", true, false), ("insert_colorizer_off", false, false), ("performTextInput_colorizer_on", true, true), ("performTextInput_colorizer_off", false, true) })
        {
            window.Live.Set(on);
            var runs = new List<double>();
            var parse = new List<double>();
            for (var i = 0; i < 10; i++)
            {
                editor.Document.Text = "";
                await Settle();
                editor.TextArea.Caret.Offset = 0;
                var sw = Stopwatch.StartNew();
                if (viaInput)
                {
                    editor.TextArea.PerformTextInput(text);
                }
                else
                {
                    editor.Document.Insert(0, text);
                }
                editor.TextArea.TextView.EnsureVisualLines();
                await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Render);
                runs.Add(sw.Elapsed.TotalMilliseconds);
                parse.Add(window.Live.Spans.LastParseMs);
            }
            runs.Sort();
            parse.Sort();
            w.WriteStartObject(name);
            w.WriteNumber("median_ms", Math.Round(runs[runs.Count / 2], 1));
            w.WriteNumber("max_ms", Math.Round(runs[^1], 1));
            w.WriteNumber("min_ms", Math.Round(runs[0], 1));
            w.WriteNumber("last_markdig_parse_median_ms", Math.Round(parse[parse.Count / 2], 1));
            w.WriteNumber("doc_lines_after", editor.Document.LineCount);
            w.WriteEndObject();
        }
        w.WriteEndObject();
        window.Live.Set(true);
    }

    private static async Task Caret(MainWindow window, Utf8JsonWriter w)
    {
        var editor = window.Editor;
        var area = editor.TextArea;
        window.Live.Set(true);
        editor.Document.Text = ChipLine + "\nsecond line\n";
        await Settle();
        var firstEnd = ChipLine.IndexOf("]]", StringComparison.Ordinal) + 2;
        var secondEnd = ChipLine.LastIndexOf("]]", StringComparison.Ordinal) + 2;
        var cases = new (string Name, int Offset)[] { ("before_first_chip", 3), ("end_of_first_chip", firstEnd), ("between_chips", firstEnd + 3), ("after_second_chip", secondEnd + 2) };
        w.WriteStartArray("caret");
        foreach (var (name, offset) in cases)
        {
            area.Caret.Offset = offset;
            await Settle();
            var before = Snap(area);
            area.Caret.Line = 2;
            area.Caret.Column = 1;
            await Settle();
            var away = Snap(area);
            area.Caret.Offset = offset;
            await Settle();
            var back = Snap(area);
            WriteCase(w, name, offset, before, away, back);
        }
        // Click at the right edge of the first chip while the line shows chips (caret on line 2).
        area.Caret.Line = 2;
        area.Caret.Column = 1;
        await Settle();
        var tv = area.TextView;
        tv.EnsureVisualLines();
        var edge = tv.GetVisualPosition(new TextViewPosition(1, 1 + firstEnd), AvaloniaEdit.Rendering.VisualYPosition.LineMiddle);
        var hit = tv.GetPosition(new Avalonia.Point(edge.X - 0.5 - tv.ScrollOffset.X, edge.Y - tv.ScrollOffset.Y));
        var hitOffset = hit is { } p ? editor.Document.GetOffset(p.Line, p.Column) : -1;
        var beforeClick = Snap(area);
        if (hit is { } pos)
        {
            area.Caret.Position = pos;
        }
        await Settle();
        var afterClick = Snap(area);
        w.WriteStartObject();
        w.WriteString("name", "click_right_edge_of_first_chip");
        w.WriteNumber("expected_offset", firstEnd);
        w.WriteNumber("hit_test_offset", hitOffset);
        w.WriteNumber("caret_offset_after", afterClick.Offset);
        w.WriteNumber("caret_line_after", afterClick.Line);
        w.WriteNumber("caret_offset_before", beforeClick.Offset);
        w.WriteBoolean("same_offset", afterClick.Offset == firstEnd);
        w.WriteEndObject();
        w.WriteEndArray();
    }

    private static (int Offset, int Line, int Column, int VisualColumn) Snap(AvaloniaEdit.Editing.TextArea area) =>
        (area.Caret.Offset, area.Caret.Line, area.Caret.Column, area.Caret.Position.VisualColumn);

    private static void WriteCase(Utf8JsonWriter w, string name, int offset, (int Offset, int Line, int Column, int VisualColumn) before, (int Offset, int Line, int Column, int VisualColumn) away, (int Offset, int Line, int Column, int VisualColumn) back)
    {
        w.WriteStartObject();
        w.WriteString("name", name);
        w.WriteNumber("offset", offset);
        w.WriteNumber("offset_before", before.Offset);
        w.WriteNumber("offset_after_return", back.Offset);
        w.WriteNumber("line_away", away.Line);
        w.WriteNumber("column_before", before.Column);
        w.WriteNumber("column_after_return", back.Column);
        w.WriteNumber("visual_column_before", before.VisualColumn);
        w.WriteNumber("visual_column_after_return", back.VisualColumn);
        w.WriteBoolean("same_offset", before.Offset == back.Offset);
        w.WriteEndObject();
    }

    private static async Task Bytes(MainWindow window, Utf8JsonWriter w, string outDir)
    {
        var editor = window.Editor;
        window.Live.Set(true);
        w.WriteStartArray("n1b");
        foreach (var (name, original) in Fixtures.All)
        {
            var path = Path.Combine(outDir, name);
            await File.WriteAllBytesAsync(path, original);
            var disk = await File.ReadAllBytesAsync(path);
            var doc = ByteDoc.Read(disk) ?? throw new InvalidOperationException(name + " is not UTF-8");
            editor.Document.Text = doc.Text;
            await Settle();
            // One typed character through the editor's own input path, after the first character.
            editor.TextArea.Caret.Offset = Math.Min(1, editor.Document.TextLength);
            var typedAt = editor.TextArea.Caret.Offset;
            editor.TextArea.PerformTextInput("X");
            await Settle();
            var saved = doc.ToBytes(editor.Document.Text);
            var byteIndex = (doc.HasBom ? 3 : 0) + Encoding.UTF8.GetByteCount(doc.Text.AsSpan(0, typedAt));
            var expected = new List<byte>(original);
            expected.Insert(byteIndex, (byte)'X');
            var identical = saved.AsSpan().SequenceEqual(expected.ToArray());
            // Enter at the end of line 1 and at the end of the document: which ending does the editor insert?
            var enter1 = EnterBytes(editor, doc, atDocumentEnd: false);
            var enterEnd = EnterBytes(editor, doc, atDocumentEnd: true);
            w.WriteStartObject();
            w.WriteString("fixture", name);
            w.WriteBoolean("bom", doc.HasBom);
            w.WriteNumber("original_bytes", original.Length);
            w.WriteNumber("saved_bytes", saved.Length);
            w.WriteBoolean("one_char_only", identical);
            w.WriteString("enter_after_line1_inserted_hex", enter1);
            w.WriteString("enter_at_end_inserted_hex", enterEnd);
            w.WriteEndObject();
        }
        w.WriteEndArray();
    }

    private static string EnterBytes(TextEditor editor, ByteDoc doc, bool atDocumentEnd)
    {
        editor.Document.Text = doc.Text;
        var area = editor.TextArea;
        area.Caret.Offset = atDocumentEnd ? editor.Document.TextLength : editor.Document.GetLineByNumber(1).EndOffset;
        var before = editor.Document.TextLength;
        var at = area.Caret.Offset;
        area.PerformTextInput("\n");
        var added = editor.Document.TextLength - before;
        return Convert.ToHexString(Encoding.UTF8.GetBytes(editor.Document.GetText(at, Math.Max(0, added))));
    }
}
