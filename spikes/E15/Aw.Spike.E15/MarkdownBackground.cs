using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace Aw.Spike.E15;

/// <summary>Draws fills behind code blocks, tables and quotes. Draw only, no layout change.</summary>
internal sealed class MarkdownBackground(MdSpans spans) : IBackgroundRenderer
{
    private static readonly IBrush Code = new SolidColorBrush(Color.FromArgb(0x22, 0x80, 0x80, 0x80));
    private static readonly IBrush Table = new SolidColorBrush(Color.FromArgb(0x18, 0x1F, 0x6F, 0xEB));
    private static readonly IBrush Quote = new SolidColorBrush(Color.FromArgb(0x30, 0xE0, 0xA0, 0x20));

    public KnownLayer Layer => KnownLayer.Background;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!textView.VisualLinesValid || textView.VisualLines.Count == 0)
        {
            return;
        }
        foreach (var s in spans.Current)
        {
            IBrush? brush = s.Kind switch
            {
                MdKind.CodeBlock => Code,
                MdKind.Table => Table,
                MdKind.Quote => Quote,
                _ => null,
            };
            if (brush is null)
            {
                continue;
            }
            var builder = new BackgroundGeometryBuilder { AlignToWholePixels = true };
            builder.AddSegment(textView, new TextSegment { StartOffset = s.Start, EndOffset = s.End });
            var geometry = builder.CreateGeometry();
            if (geometry is not null)
            {
                drawingContext.DrawGeometry(brush, null, geometry);
            }
        }
    }
}
