using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Aw.Sindri.Ui.ViewModels.Rows;

namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>The text of a flat <see cref="Row"/> (#3189): the layouts it draws, built once per item and kept until something they depend on changes.</summary>
internal sealed partial class Row
{
    private const double Gap = 12;
    private const double TitleGap = 6;
    private const double MonoChar = 7.2;   // JetBrains Mono at 12 px: RefFit's fallback when it can't measure
    private const int Id = 0;
    private const int Title = 1;
    private const int Sub = 2;
    private const int Age = 3;
    private const int Wait = 4;

    private static readonly string[] _toneKeys = ["Neutral", "Ok", "Attention", "Error", "Running", "Waiting", "Draft"];

    private readonly TextLayout?[] _text = new TextLayout?[5];
    private readonly ChipBrushes?[] _chips = new ChipBrushes?[7];
    private TextLayout?[] _facts = [];
    private string?[] _cellText = [];
    private Metrics? _metrics;
    private FontSet? _fonts;
    private Inks? _inks;
    private double _titleRoom = double.NaN;
    private double _titleDesired;
    private bool _titleTrimmed;
    private IBrush? _titleInk;
    private int _shown;
    private Slots _slots;
    private int _layoutsBuilt;

    private sealed record Metrics(double Inset, double AgeWidth, double WaitWidth, double TitleMin, double ChipRadius, double[] Columns);

    private sealed record FontSet(Typeface Text, Typeface Medium, Typeface Bold, Typeface Mono, Typeface Chip, double Body, double Caption, double LineHeight, double MonoSize);

    private sealed record Inks(IBrush Ink, IBrush Muted, IBrush Subtle, IBrush Surface3, IBrush Primary);

    private sealed record ChipBrushes(IBrush Fill, IBrush Ink, IBrush Border);

    // How a text is cut: the width it may take (an ellipsis ends it) and how it sits in that width.
    private sealed record Fit(double MaxWidth, TextAlignment Align = TextAlignment.Left);

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct Slots(double TitleX, double TitleWidth, double FactsX, double AgeX, double WaitX);

    // Everything the layouts, fonts and brushes were built from can change: the item, the columns, the theme, the text scale and the render scaling. Each one drops them all.
    private void DropLayouts()
    {
        Array.Clear(_text);
        Array.Clear(_facts);
        Array.Clear(_chips);
        _metrics = null;
        _fonts = null;
        _inks = null;
        _palette = null;
        _selBar = null;
        _ring = null;
        _titleRoom = double.NaN;
        InvalidateVisual();
    }

    private Metrics MetricsNow() => _metrics ??= new Metrics(
        Resource("Row.Inset", 24),
        Resource("Row.Col.Age", 44),
        Resource("Row.Col.Wait", 112),
        Resource("Row.Col.TitleMin", 200),
        this.TryFindResource("Radius.SM", ActualThemeVariant, out var radius) && radius is CornerRadius corner ? corner.TopLeft : 6,
        [.. (Columns ?? []).Select(c => Resource(c.Wide ? "Row.Col.FactWide" : "Row.Col.Fact", c.Wide ? 136 : 104))]);

    private FontSet FontsNow() => _fonts ??= NewFonts();

    private FontSet NewFonts()
    {
        var text = GetValue(TextElement.FontFamilyProperty);
        var mono = FamilyOf("Font.Mono", text);
        var chip = FamilyOf("Font.Text", text);
        return new FontSet(
            new Typeface(text),
            new Typeface(text, FontStyle.Normal, FontWeight.Medium),
            new Typeface(text, FontStyle.Normal, FontWeight.Bold),
            new Typeface(mono),
            new Typeface(chip, FontStyle.Normal, FontWeight.Medium),
            Resource("Type.Body.Size", 13),
            Resource("Type.Caption.Size", 12),
            Resource("Type.Caption.LineHeight", 18),
            Resource("Type.Mono.Size", 12));
    }

    private FontFamily FamilyOf(string key, FontFamily fallback) => this.TryFindResource(key, ActualThemeVariant, out var found) && found is FontFamily family ? family : fallback;

    private Inks InksNow() => _inks ??= new Inks(Brush("Brush.Ink"), Brush("Brush.InkMuted"), Brush("Brush.InkSubtle"), Brush("Brush.Surface3"), Brush("Brush.Primary"));

    private ChipBrushes Chip(RowTone tone) => _chips[(int)tone] ??= NewChip(_toneKeys[(int)tone]);

    private ChipBrushes NewChip(string key) => new(Brush("Brush.Status." + key + ".Bg"), Brush("Brush.Status." + key + ".Fg"), Brush("Brush.Status." + key + ".Border"));

    private TextLayout Make(string text, Typeface face, double size, IBrush ink, Fit? fit = null)
    {
        _layoutsBuilt++;
        return fit is null
            ? new TextLayout(text, face, size, ink, textWrapping: TextWrapping.NoWrap)
            : new TextLayout(text, face, size, ink, textAlignment: fit.Align, textWrapping: TextWrapping.NoWrap, textTrimming: TextTrimming.CharacterEllipsis, maxWidth: fit.MaxWidth);
    }

    // The layout Prepare built for a slot of the title line, age or wait.
    private TextLayout Built(int slot) => _text[slot] ?? throw new InvalidOperationException("Prepare builds every text of the row before it is drawn.");

    private TextLayout BuiltFact(int slot) => _facts[slot] ?? throw new InvalidOperationException("Prepare builds every fact of the row before it is drawn.");

    // Lays out every text of the row for its size and keeps what is still good. Render draws from these fields and the Layout hook reads them.
    private void Prepare(IRowItem item, Size size)
    {
        var m = MetricsNow();
        _shown = Math.Min(m.Columns.Length, VisibleColumns);
        var factsWidth = 0d;
        for (var i = 0; i < _shown; i++)
        {
            factsWidth += Gap + m.Columns[i];
        }

        var titleX = m.Inset + GlyphBox + Gap;
        var ageX = size.Width - m.Inset - m.WaitWidth - Gap - m.AgeWidth;
        var factsX = ageX - Gap - factsWidth;
        _slots = new Slots(titleX, Math.Max(m.TitleMin, factsX - titleX), factsX, ageX, size.Width - m.Inset - m.WaitWidth);
        BuildTitleLine(item);
        BuildAgeAndWait(item, m);
        BuildFacts(item, m);
    }

    private void BuildTitleLine(IRowItem item)
    {
        var f = FontsNow();
        var k = InksNow();
        var id = _text[Id] ??= Make(item.Id, f.Mono, f.MonoSize, k.Subtle);
        var sub = string.IsNullOrEmpty(item.SubLine) ? null : _text[Sub] ??= Make(item.SubLine, f.Text, f.Caption, k.Subtle);
        var room = Math.Max(0, _slots.TitleWidth - id.Width - TitleGap - (sub is null ? 0 : TitleGap + sub.Width));
        if (_text[Title] is not null && _titleRoom.Equals(room))
        {
            return;
        }

        var face = item.Unread ? f.Medium : f.Text;
        _titleInk = item.Unread ? k.Ink : k.Muted;
        var full = Make(item.Title, face, f.Body, _titleInk);
        _titleDesired = full.Width;
        _titleTrimmed = full.Width > room + 0.01;
        _text[Title] = _titleTrimmed ? Make(item.Title, face, f.Body, _titleInk, new Fit(room)) : full;
        _titleRoom = room;
    }

    private void BuildAgeAndWait(IRowItem item, Metrics m)
    {
        var f = FontsNow();
        var k = InksNow();
        _text[Age] ??= Make(item.Age ?? string.Empty, f.Text, f.Caption, k.Subtle, new Fit(m.AgeWidth, TextAlignment.Right));
        var you = item.WaitsOn is { Who: RowWho.Me };
        _text[Wait] ??= Make(WaitText(item), you ? f.Bold : f.Text, f.Caption, you ? k.Ink : k.Muted, new Fit(m.WaitWidth));
    }

    private static string WaitText(IRowItem item) => item.WaitsOn switch
    {
        { Who: RowWho.Me } => "you",
        { Who: not RowWho.Nobody } waits => waits.Name,
        _ => string.Empty,
    };
}
