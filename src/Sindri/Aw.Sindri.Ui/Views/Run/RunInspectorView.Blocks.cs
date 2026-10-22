using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Aw.Sindri.Ui.ViewModels.Run;
using Aw.Sindri.Ui.Views.Icons;

namespace Aw.Sindri.Ui.Views.Run;

/// <summary>The inspector's lists (<see cref="RunInspectorView"/>, continued): the step timeline, the facts and the disclosures.</summary>
internal sealed partial class RunInspectorView
{
    private StackPanel Timeline(RunCanvasInspectorViewModel inspector)
    {
        var rows = new StackPanel { Name = "RunInspectorTimeline", Spacing = 8 };
        foreach (var row in inspector.Timeline)
        {
            rows.Children.Add(TimelineRow(row));
        }

        if (inspector.TimelineNote.Length > 0)
        {
            rows.Children.Add(Wrapped(inspector.TimelineNote, "Brush.InkSubtle"));
        }

        return Block(inspector.TimelineHeading, rows);
    }

    private Border TimelineRow(RunTimelineRow row)
    {
        var top = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        top.Children.Add(new StateIcon { Icon = row.Icon, Foreground = this.Brush(row.TextBrush), Spins = row.IsRunning, VerticalAlignment = VerticalAlignment.Center });
        top.Children.Add(RunResources.Text(row.TryText, 13, 18, FontWeight.Medium, this.Brush("Brush.Ink")));
        top.Children.Add(RunResources.Text(row.Word, 12, 16, FontWeight.Medium, this.Brush(row.TextBrush)));
        var lines = new StackPanel { Spacing = 2 };
        lines.Children.Add(top);
        lines.Children.Add(Wrapped(row.TimesText, "Brush.InkSubtle"));
        if (row.HasWho || row.HasTokens)
        {
            lines.Children.Add(Wrapped(string.Join(" · ", new[] { row.Who, row.TokensText }.Where(s => s.Length > 0)), "Brush.InkSubtle"));
        }

        if (row.HasNote)
        {
            lines.Children.Add(Wrapped(row.Note, row.TextBrush));
        }

        var border = new Border { Background = this.Brush("Brush.Surface2"), BorderBrush = this.Brush("Brush.Hairline"), BorderThickness = new Thickness(1), CornerRadius = this.Corner("Radius.SM", 6), Padding = new Thickness(10, 8), Child = lines };
        AutomationProperties.SetName(border, row.AutomationName);
        AutomationProperties.SetAccessibilityView(border, AccessibilityView.Control);
        AutomationProperties.SetControlTypeOverride(border, Avalonia.Automation.Peers.AutomationControlType.Group);
        return border;
    }

    private StackPanel Facts(string heading, string whole, IReadOnlyList<RunFactItem> facts)
    {
        var list = new StackPanel { Spacing = 6 };
        list.Children.Add(RunResources.Text(whole, 11, 14, FontWeight.Normal, this.Brush("Brush.InkSubtle")));
        foreach (var fact in facts)
        {
            var line = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
            line.Children.Add(Wrapped(fact.Summary.Length > 0 ? fact.Label + ": " + fact.Summary : fact.Label, "Brush.Ink"));
            var word = RunResources.Text(fact.Word, 12, 16, FontWeight.Medium, this.Brush(fact.Brush));
            Grid.SetColumn(word, 1);
            line.Children.Add(word);
            AutomationProperties.SetName(line, fact.Label + ", " + fact.Word);
            list.Children.Add(line);
        }

        return Block(heading, list);
    }

    private Expander Disclosure(string heading, IReadOnlyList<string> lines, string name)
    {
        var stack = new StackPanel { Spacing = 4 };
        foreach (var line in lines)
        {
            var text = Wrapped(line, "Brush.InkSubtle");
            text.Classes.Add("mono");
            stack.Children.Add(text);
        }

        var expander = new Expander { Name = name, Header = heading, IsExpanded = false, Content = stack };
        AutomationProperties.SetName(expander, heading);
        return expander;
    }
}
