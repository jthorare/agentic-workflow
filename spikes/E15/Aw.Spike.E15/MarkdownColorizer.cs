using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace Aw.Spike.E15;

/// <summary>Styles text in place (size, weight, font, colour); offsets map 1:1 so the caret never moves.</summary>
internal sealed class MarkdownColorizer(MdSpans spans, double baseSize) : DocumentColorizingTransformer
{
    private static readonly FontFamily Mono = new("Cascadia Code,Menlo,DejaVu Sans Mono,monospace");
    private static readonly IBrush Muted = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80));
    private static readonly IBrush Accent = new SolidColorBrush(Color.FromRgb(0x1F, 0x6F, 0xEB));
    private static readonly double[] HeadingScale = [1, 1.8, 1.5, 1.3, 1.15, 1.05, 1];

    protected override void ColorizeLine(DocumentLine line)
    {
        foreach (var s in spans.Current)
        {
            if (s.Start > line.EndOffset)
            {
                break;
            }
            if (s.End <= line.Offset)
            {
                continue;
            }
            var a = Math.Max(s.Start, line.Offset);
            var b = Math.Min(s.End, line.EndOffset);
            if (b <= a)
            {
                continue;
            }
            var kind = s.Kind;
            var level = s.Level;
            ChangeLinePart(a, b, e => Style(e, kind, level));
        }
    }

    private void Style(VisualLineElement e, MdKind kind, int level)
    {
        var p = e.TextRunProperties;
        switch (kind)
        {
            case MdKind.Heading:
                p.SetFontRenderingEmSize(baseSize * HeadingScale[Math.Clamp(level, 1, 6)]);
                p.SetTypeface(new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold));
                break;
            case MdKind.Bold:
                p.SetTypeface(new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold));
                break;
            case MdKind.Italic:
                p.SetTypeface(new Typeface(FontFamily.Default, FontStyle.Italic, FontWeight.Normal));
                break;
            case MdKind.CodeInline:
            case MdKind.CodeBlock:
                p.SetTypeface(new Typeface(Mono));
                break;
            case MdKind.TaskDone:
                p.SetForegroundBrush(Muted);
                break;
            case MdKind.Link:
                p.SetForegroundBrush(Accent);
                break;
            case MdKind.Quote:
                p.SetForegroundBrush(Muted);
                break;
        }
    }
}
