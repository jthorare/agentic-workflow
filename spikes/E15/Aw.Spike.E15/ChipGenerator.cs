using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;

namespace Aw.Spike.E15;

/// <summary>Shows [[issue 123]] / [[PR 9]] as a chip on every line except the caret line, where the raw text shows (spec decision 12).</summary>
internal sealed partial class ChipGenerator : VisualLineElementGenerator
{
    private readonly TextEditor _editor;
    private int _caretLine;

    public ChipGenerator(TextEditor editor)
    {
        _editor = editor;
        _caretLine = editor.TextArea.Caret.Line;
        editor.TextArea.Caret.PositionChanged += (_, _) =>
        {
            var now = editor.TextArea.Caret.Line;
            var old = _caretLine;
            _caretLine = now;
            if (now != old)
            {
                RedrawLine(old);
                RedrawLine(now);
            }
        };
    }

    [GeneratedRegex(@"\[\[(?<kind>issue|PR) (?<n>\d+)\]\]", RegexOptions.CultureInvariant)]
    private static partial Regex ChipPattern();

    private void RedrawLine(int number)
    {
        var doc = _editor.Document;
        if (doc is not null && number >= 1 && number <= doc.LineCount)
        {
            _editor.TextArea.TextView.Redraw(doc.GetLineByNumber(number));
        }
    }

    private Match? Find(int startOffset)
    {
        var line = CurrentContext.Document.GetLineByOffset(startOffset);
        if (line.LineNumber == _caretLine)
        {
            return null;
        }
        var text = CurrentContext.Document.GetText(startOffset, line.EndOffset - startOffset);
        var m = ChipPattern().Match(text);
        return m.Success ? m : null;
    }

    public override int GetFirstInterestedOffset(int startOffset)
    {
        var m = Find(startOffset);
        return m is null ? -1 : startOffset + m.Index;
    }

    public override VisualLineElement? ConstructElement(int offset)
    {
        var m = Find(offset);
        if (m is null || m.Index != 0)
        {
            return null;
        }
        var chip = new Border
        {
            Background = m.Groups["kind"].Value == "PR" ? Brushes.MediumPurple : Brushes.SteelBlue,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(6, 0),
            Child = new TextBlock { Text = $"{m.Groups["kind"].Value} #{m.Groups["n"].Value}", Foreground = Brushes.White, FontSize = 12 },
        };
        return new InlineObjectElement(m.Length, chip);
    }
}
