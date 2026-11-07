using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>A layout check over the text under a control (#2228): two visible texts side by side under one parent must keep a gap, and none may run past the control's right edge.</summary>
internal static class TextRowCheck
{
    /// <summary>The smallest gap, in px, two texts on one row must keep.</summary>
    internal const double MinGap = 4;

    /// <summary>Every touching pair and every text cut off by the right edge of <paramref name="root"/>, as readable lines; empty when the layout holds.</summary>
    /// <param name="root">The control whose texts are checked, already laid out.</param>
    /// <returns>One line per problem, naming the texts and their rectangles.</returns>
    internal static IReadOnlyList<string> Problems(Control root)
    {
        var texts = root.GetVisualDescendants().OfType<TextBlock>()
            .Where(t => t.IsEffectivelyVisible && !string.IsNullOrEmpty(t.Text) && t.Bounds.Width > 0)
            .Select(t => (Text: t, Rect: new Rect(t.TranslatePoint(default, root) ?? default, t.Bounds.Size)))
            .ToList();
        return [.. CutOff(root, texts), .. Touching(texts)];
    }

    /// <summary>Every visible text or button under <paramref name="root"/> whose right edge is past the control's width, as readable lines; empty when nothing is cut off.</summary>
    /// <param name="root">The control whose texts and buttons are checked, already laid out.</param>
    /// <returns>One line per cut-off control, naming it and its rectangle.</returns>
    internal static IReadOnlyList<string> CutOffControls(Control root) =>
        [.. root.GetVisualDescendants().Where(c => c is TextBlock or Button).Cast<Control>()
            .Where(c => c.IsEffectivelyVisible && c.Bounds.Width > 0 && (c is not TextBlock t || !string.IsNullOrEmpty(t.Text)))
            .Select(c => (Control: c, Rect: new Rect(c.TranslatePoint(default, root) ?? default, c.Bounds.Size)))
            .Where(c => c.Rect.Right > root.Bounds.Width + 0.5)
            .Select(c => string.Create(CultureInfo.InvariantCulture, $"{c.Control.GetType().Name} '{c.Control.Name ?? (c.Control as TextBlock)?.Text}' {c.Rect} runs past the right edge ({root.Bounds.Width})"))];

    private static IEnumerable<string> CutOff(Control root, List<(TextBlock Text, Rect Rect)> texts) =>
        texts.Where(t => t.Rect.Right > root.Bounds.Width + 0.5)
            .Select(t => string.Create(CultureInfo.InvariantCulture, $"'{Short(t.Text)}' {t.Rect} runs past the right edge ({root.Bounds.Width})"));

    private static IEnumerable<string> Touching(List<(TextBlock Text, Rect Rect)> texts)
    {
        for (var i = 0; i < texts.Count; i++)
        {
            for (var j = i + 1; j < texts.Count; j++)
            {
                var (a, b) = (texts[i], texts[j]);
                var sameRow = a.Text.GetVisualParent() == b.Text.GetVisualParent() && a.Rect.Top < b.Rect.Bottom && b.Rect.Top < a.Rect.Bottom;
                var gap = Math.Max(a.Rect.Left, b.Rect.Left) - Math.Min(a.Rect.Right, b.Rect.Right);
                if (sameRow && gap < MinGap)
                {
                    yield return string.Create(CultureInfo.InvariantCulture, $"'{Short(a.Text)}' {a.Rect} and '{Short(b.Text)}' {b.Rect} are {gap} px apart (need {MinGap})");
                }
            }
        }
    }

    private static string Short(TextBlock t) => t.Text!.Length > 24 ? t.Text[..24] : t.Text;
}
