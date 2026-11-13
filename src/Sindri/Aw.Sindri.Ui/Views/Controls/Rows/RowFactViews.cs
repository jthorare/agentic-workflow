using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Aw.Sindri.Ui.ViewModels.Rows;

namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>Builds the cell of one fact (#2978 grammar): chip, 56 by 4 progress bar, diffstat, ref, person, count or text. Every cell ellipsizes.</summary>
[SuppressMessage("Design", "MA0182", Justification = "Used by the Frames tests until the flat Row slices move them (#3189 slice 4).")]
internal static class RowFactViews
{
    private const double MonoCharWidth = 7.2;   // JetBrains Mono at 12 px

    /// <summary>How many cell contents were ever built: the recycle path reuses them, so this grows with the containers made and not with the rows scrolled past.</summary>
    internal static long ContentsBuilt => Interlocked.Read(ref _built);

    private static long _built;

    /// <summary>Shows a fact in a cell. A cell that already holds the content for this kind of fact keeps it and only sets text and a few properties; otherwise it builds the content once.</summary>
    /// <param name="cell">The column's cell.</param>
    /// <param name="fact">The fact; null empties the cell.</param>
    /// <param name="width">The column width, to shorten a ref to.</param>
    public static void Update(Border cell, RowFact? fact, double width)
    {
        ArgumentNullException.ThrowIfNull(cell);
        if (fact is null)
        {
            cell.Child = null;
            return;
        }

        if (cell.Child is not Control content || content.Tag is not RowFactKind kind || kind != fact.Kind)
        {
            content = Create(fact.Kind);
            content.Tag = fact.Kind;
            cell.Child = content;
            _ = Interlocked.Increment(ref _built);
        }

        Fill(content, fact, width);
    }

    private static Control Create(RowFactKind kind) => kind switch
    {
        RowFactKind.Chip => NewChip(),
        RowFactKind.Progress => NewProgress(),
        RowFactKind.Person => NewPerson(),
        RowFactKind.Diffstat or RowFactKind.Ref => Text(string.Empty, mono: true),
        RowFactKind.Text or RowFactKind.Count => Text(string.Empty, mono: false),
        _ => Text(string.Empty, mono: false),
    };

    private static void Fill(Control content, RowFact fact, double width)
    {
        switch (fact.Kind)
        {
            case RowFactKind.Chip:
                FillChip((Border)content, fact);
                break;
            case RowFactKind.Progress:
                FillProgress((Grid)content, fact);
                break;
            case RowFactKind.Person:
                FillPerson((Grid)content, fact.Text);
                break;
            case RowFactKind.Ref:
                ((TextBlock)content).Text = RefFit.Fit(content, fact.Text, width, MonoCharWidth);
                break;
            case RowFactKind.Text or RowFactKind.Diffstat or RowFactKind.Count:
            default:
                ((TextBlock)content).Text = fact.Text;
                break;
        }
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

    private static Border NewChip()
    {
        var chip = new Border { Child = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap } };
        chip.Classes.Add("status-chip");
        return chip;
    }

    // The tone comes from the fact; the chip never reads it out of the text.
    private static readonly string[] _toneClasses = ["neutral", "ok", "attention", "error", "running", "waiting", "draft"];

    private static void FillChip(Border chip, RowFact fact)
    {
        if (chip.Child is TextBlock label)
        {
            label.Text = fact.Text;
        }

        var tone = _toneClasses[(int)fact.Tone];
        foreach (var name in _toneClasses)
        {
            chip.Classes.Set(name, name == tone);
        }
    }

    private static Grid NewProgress()
    {
        var bar = new Border { Width = 56, Height = 4, CornerRadius = new CornerRadius(2), ClipToBounds = true, VerticalAlignment = VerticalAlignment.Center };
        _ = bar.Bind(Border.BackgroundProperty, bar.GetResourceObservable("Brush.Surface3"));
        var fill = new Border { Width = 0, HorizontalAlignment = HorizontalAlignment.Left };
        _ = fill.Bind(Border.BackgroundProperty, fill.GetResourceObservable("Brush.Primary"));
        bar.Child = fill;
        var count = Text(string.Empty, mono: false);
        count.Margin = new Thickness(8, 0, 0, 0);
        Grid.SetColumn(count, 1);
        return new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Children = { bar, count } };
    }

    private static void FillProgress(Grid grid, RowFact fact)
    {
        var share = fact.Total > 0 ? Math.Clamp((double)fact.Done / fact.Total, 0, 1) : 0;
        if (grid.Children[0] is Border { Child: Border fill })
        {
            fill.Width = 56 * share;
        }

        ((TextBlock)grid.Children[1]).Text = fact.Text;
    }

    private static Grid NewPerson()
    {
        var avatar = new Border
        {
            Width = 16,
            Height = 16,
            CornerRadius = new CornerRadius(8),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { FontSize = 9, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
        };
        _ = avatar.Bind(Border.BackgroundProperty, avatar.GetResourceObservable("Brush.Surface3"));
        var label = Text(string.Empty, mono: false);
        label.Margin = new Thickness(6, 0, 0, 0);
        Grid.SetColumn(label, 1);
        return new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Children = { avatar, label } };
    }

    private static void FillPerson(Grid grid, string name)
    {
        if (grid.Children[0] is Border { Child: TextBlock initial })
        {
            initial.Text = name.Length > 0 ? name[..1].ToUpperInvariant() : string.Empty;
        }

        ((TextBlock)grid.Children[1]).Text = name;
    }
}
