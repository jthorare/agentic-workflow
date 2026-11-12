using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Aw.Sindri.Ui.ViewModels.Rows;

namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>Builds the cell of one fact (#2978 grammar): chip, 56 by 4 progress bar, diffstat, ref, person, count or text. Every cell ellipsizes.</summary>
internal static class RowFactViews
{
    private const double MonoCharWidth = 7.2;   // JetBrains Mono at 12 px

    /// <summary>Builds the content of a fact cell.</summary>
    /// <param name="fact">The fact.</param>
    /// <param name="width">The column width, to shorten a ref to.</param>
    /// <returns>The content; the cell clips it.</returns>
    public static Control Build(RowFact fact, double width)
    {
        ArgumentNullException.ThrowIfNull(fact);
        return fact.Kind switch
        {
            RowFactKind.Chip => Chip(fact.Text),
            RowFactKind.Progress => Progress(fact),
            RowFactKind.Diffstat => Text(fact.Text, mono: true),
            RowFactKind.Ref => Text(RefText.ShortenMiddle(fact.Text, Math.Max(4, (int)(width / MonoCharWidth))), mono: true),
            RowFactKind.Person => Person(fact.Text),
            RowFactKind.Text or RowFactKind.Count => Text(fact.Text, mono: false),
            _ => Text(fact.Text, mono: false),
        };
    }

    private static TextBlock Text(string text, bool mono)
    {
        var block = new TextBlock
        {
            Text = text,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _ = block.Bind(TextBlock.ForegroundProperty, block.GetResourceObservable("Brush.InkSubtle"));
        _ = block.Bind(TextBlock.FontSizeProperty, block.GetResourceObservable("Type.Caption.Size"));
        if (mono)
        {
            _ = block.Bind(TextBlock.FontFamilyProperty, block.GetResourceObservable("Font.Mono"));
        }

        return block;
    }

    private static Border Chip(string text)
    {
        var tone = text.ToLowerInvariant() switch
        {
            var t when t.Contains("fail", StringComparison.Ordinal) || t.Contains("error", StringComparison.Ordinal) => "error",
            var t when t.Contains("running", StringComparison.Ordinal) => "running",
            var t when t.Contains("done", StringComparison.Ordinal) || t.Contains("merged", StringComparison.Ordinal) || t == "ok" => "ok",
            var t when t.Contains("draft", StringComparison.Ordinal) => "draft",
            var t when t.Contains("review", StringComparison.Ordinal) || t.Contains("wait", StringComparison.Ordinal) => "waiting",
            var t when t.Contains("blocked", StringComparison.Ordinal) || t.Contains("parked", StringComparison.Ordinal) => "attention",
            _ => "neutral",
        };
        var chip = new Border { Child = new TextBlock { Text = text, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap } };
        chip.Classes.Add("status-chip");
        chip.Classes.Add(tone);
        return chip;
    }

    private static Grid Progress(RowFact fact)
    {
        var bar = new Border { Width = 56, Height = 4, CornerRadius = new CornerRadius(2), ClipToBounds = true, VerticalAlignment = VerticalAlignment.Center };
        _ = bar.Bind(Border.BackgroundProperty, bar.GetResourceObservable("Brush.Surface3"));
        var share = fact.Total > 0 ? Math.Clamp((double)fact.Done / fact.Total, 0, 1) : 0;
        var fill = new Border { Width = 56 * share, HorizontalAlignment = HorizontalAlignment.Left };
        _ = fill.Bind(Border.BackgroundProperty, fill.GetResourceObservable("Brush.Primary"));
        bar.Child = fill;
        var count = Text(fact.Text, mono: false);
        count.Margin = new Thickness(8, 0, 0, 0);
        Grid.SetColumn(count, 1);
        return new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Children = { bar, count } };
    }

    private static Grid Person(string name)
    {
        var avatar = new Border
        {
            Width = 16,
            Height = 16,
            CornerRadius = new CornerRadius(8),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = name.Length > 0 ? name[..1].ToUpperInvariant() : string.Empty, FontSize = 9, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
        };
        _ = avatar.Bind(Border.BackgroundProperty, avatar.GetResourceObservable("Brush.Surface3"));
        var label = Text(name, mono: false);
        label.Margin = new Thickness(6, 0, 0, 0);
        Grid.SetColumn(label, 1);
        return new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Children = { avatar, label } };
    }
}
