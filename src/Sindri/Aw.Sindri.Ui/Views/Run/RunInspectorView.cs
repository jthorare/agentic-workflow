using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Aw.Sindri.Ui.ViewModels.Run;
using Aw.Sindri.Ui.Views.Components;
using Aw.Sindri.Ui.Views.Icons;

namespace Aw.Sindri.Ui.Views.Run;

/// <summary>
/// The run canvas's inspector (F3 spec 3 §2, slice 4): a header with the step's name, state and the close, previous and next buttons, then the
/// timeline, why, what it needs and made, how it ended, what you can do next and two disclosures. Built in code because each state names its
/// brushes by key. It reads <see cref="RunCanvasInspectorViewModel"/> and sends nothing.
/// </summary>
internal sealed partial class RunInspectorView : UserControl
{
    private const double Gap = 16;

    /// <summary>Draws an inspector, or nothing for null.</summary>
    /// <param name="inspector">The inspector to draw.</param>
    internal void Show(RunCanvasInspectorViewModel? inspector) => Content = inspector is null ? null : Build(inspector);

    private Grid Build(RunCanvasInspectorViewModel inspector)
    {
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        root.Children.Add(Header(inspector));
        var body = new StackPanel { Spacing = Gap, Margin = new Thickness(Gap) };
        AddIf(body, Timeline(inspector), inspector.HasTimeline);
        body.Children.Add(Block(inspector.WhyHeading, Wrapped(inspector.WhyText, "Brush.InkSubtle")));
        AddIf(body, Facts(inspector.NeedsHeading, inspector.FactsHeading, inspector.Needs), inspector.Needs.Count > 0);
        AddIf(body, Facts(inspector.ProducedHeading, inspector.FactsHeading, inspector.Produced), inspector.Produced.Count > 0);
        AddIf(body, Block(inspector.EndHeading, Wrapped(inspector.EndNote, "Brush.Ink")), inspector.HasEnd);
        AddIf(body, Actions(inspector), inspector.HasActions);
        AddIf(body, Disclosure(inspector.TechnicalHeading, inspector.Technical, "RunTechnicalDetails"), inspector.HasTechnical);
        AddIf(body, Disclosure(inspector.RawHeading, [inspector.RawOutput], "RunRawOutput"), inspector.HasRawOutput);
        var scroll = new ScrollViewer { Content = body, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);
        return root;
    }

    private Border Header(RunCanvasInspectorViewModel inspector)
    {
        var title = RunResources.Text(inspector.Title, 16, 22, FontWeight.SemiBold, this.Brush("Brush.Ink"));
        title.Name = "RunInspectorTitle";
        var place = RunResources.Text(inspector.Place, 12, 16, FontWeight.Normal, this.Brush("Brush.InkSubtle"));
        var words = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        words.Children.Add(title);
        words.Children.Add(place);
        words.Children.Add(Badge(inspector));
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Top };
        buttons.Children.Add(Tool("RunInspectorPrevious", "↑", inspector.PreviousLabel, inspector.Previous));
        buttons.Children.Add(Tool("RunInspectorNext", "↓", inspector.NextLabel, inspector.Next));
        buttons.Children.Add(Tool("RunInspectorClose", "×", inspector.CloseLabel, inspector.Close));
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(Gap, 12) };
        grid.Children.Add(words);
        Grid.SetColumn(buttons, 1);
        grid.Children.Add(buttons);
        return new Border { Background = this.Brush("Brush.Surface1"), BorderBrush = this.Brush("Brush.Hairline"), BorderThickness = new Thickness(0, 0, 0, 1), Child = grid };
    }

    private Border Badge(RunCanvasInspectorViewModel inspector)
    {
        var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        line.Children.Add(new StateIcon { Icon = inspector.Icon, Foreground = this.Brush(inspector.TextBrush), Spins = inspector.IsRunning, VerticalAlignment = VerticalAlignment.Center });
        line.Children.Add(RunResources.Text(inspector.Word, 12, 16, FontWeight.Medium, this.Brush(inspector.TextBrush)));
        if (inspector.Kind.Length > 0)
        {
            line.Children.Add(RunResources.Text(inspector.Kind, 12, 16, FontWeight.Normal, this.Brush("Brush.InkSubtle")));
        }

        var badge = new Border { Name = "RunInspectorBadge", Background = this.Brush(inspector.SoftBrush), CornerRadius = this.Corner("Radius.Pill", 999), Padding = new Thickness(8, 2), HorizontalAlignment = HorizontalAlignment.Left, Child = line, Margin = new Thickness(0, 4, 0, 0) };
        AutomationProperties.SetName(badge, inspector.Kind.Length > 0 ? inspector.Word + ", " + inspector.Kind : inspector.Word);
        return badge;
    }

    private static void AddIf(Panel panel, Control block, bool when)
    {
        if (when)
        {
            panel.Children.Add(block);
        }
    }

    private Button Tool(string name, string glyph, string label, Action click)
    {
        var button = new Button { Name = name, Content = glyph, Padding = new Thickness(8, 2), Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = this.Brush("Brush.InkMuted") };
        AutomationProperties.SetName(button, label);
        ToolTip.SetTip(button, label);
        button.Click += (_, _) => click();
        return button;
    }

    private static StackPanel Block(string heading, Control content)
    {
        var block = new StackPanel { Spacing = 8 };
        block.Children.Add(Heading(heading));
        block.Children.Add(content);
        return block;
    }

    private static TextBlock Heading(string text)
    {
        var heading = new TextBlock { Text = text };
        heading.Classes.Add("eyebrow");
        AutomationProperties.SetName(heading, text);
        return heading;
    }

    private TextBlock Wrapped(string text, string brush)
    {
        var block = RunResources.Text(text, 13, 18, FontWeight.Normal, this.Brush(brush));
        block.TextWrapping = TextWrapping.Wrap;
        block.TextTrimming = TextTrimming.None;
        AutomationProperties.SetName(block, text);
        return block;
    }

    private StackPanel Actions(RunCanvasInspectorViewModel inspector)
    {
        var block = new StackPanel { Spacing = 12 };
        block.Children.Add(Heading(inspector.NextHeading));
        if (inspector.HasTakeOver)
        {
            block.Children.Add(ActionBlock(inspector.TakeOverTitle, inspector.TakeOverHelp, "RunInspectorTakeOver", inspector.TakeOverLabel, inspector.TakeOver));
        }

        if (inspector.Protected is { } approve)
        {
            block.Children.Add(new ProtectedActionButton { Name = "RunInspectorApprove", DataContext = approve, HorizontalAlignment = HorizontalAlignment.Left });
        }

        if (inspector.HasContext)
        {
            block.Children.Add(ActionBlock(inspector.SeeContextLabel, inspector.SeeContextHelp, "RunInspectorContext", inspector.SeeContextLabel, inspector.SeeContext));
        }

        return block;
    }

    private StackPanel ActionBlock(string title, string help, string name, string label, Action click)
    {
        var stack = new StackPanel { Spacing = 4 };
        if (title.Length > 0)
        {
            stack.Children.Add(RunResources.Text(title, 13, 18, FontWeight.Medium, this.Brush("Brush.Ink")));
        }

        if (help.Length > 0)
        {
            stack.Children.Add(Wrapped(help, "Brush.InkSubtle"));
        }

        var button = new Button { Name = name, Content = label, HorizontalAlignment = HorizontalAlignment.Left };
        AutomationProperties.SetName(button, label);
        button.Click += (_, _) => click();
        stack.Children.Add(button);
        return stack;
    }
}
