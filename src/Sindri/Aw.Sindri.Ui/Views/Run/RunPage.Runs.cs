using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Aw.Sindri.Ui.ViewModels.Run;
using Aw.Sindri.Ui.Views.Icons;

namespace Aw.Sindri.Ui.Views.Run;

/// <summary>The runs sidebar (<see cref="RunPage"/>, continued): one 56 px card per run, built in code because the state names its brush by key.</summary>
internal sealed partial class RunPage
{
    private const double CardHeight = 56;

    private Button RunsItem(RunListItemViewModel run)
    {
        var icon = new StateIcon { Icon = run.Icon, Foreground = this.Brush(run.TextBrush), Spins = run.State == RunNodeState.Running, VerticalAlignment = VerticalAlignment.Center };
        var title = RunResources.Text(run.Title, 12, 16, FontWeight.Medium, this.Brush("Brush.Ink"));
        var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        line.Children.Add(RunResources.Text(run.Word, 11, 14, FontWeight.Medium, this.Brush(run.TextBrush)));
        line.Children.Add(RunResources.Text(run.Elapsed, 11, 14, FontWeight.Normal, this.Brush("Brush.InkSubtle")));
        var words = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        words.Children.Add(title);
        words.Children.Add(line);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 10 };
        grid.Children.Add(icon);
        Grid.SetColumn(words, 1);
        grid.Children.Add(words);
        var card = new Button
        {
            Content = grid,
            Height = CardHeight,
            Padding = new Thickness(12, 10),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            CornerRadius = this.Corner("Radius.SM", 6),
            Background = this.Brush(run.IsCurrent ? "Brush.Surface3" : "Brush.Surface2"),
            BorderBrush = this.Brush(run.IsCurrent ? "Brush.HairlineStrong" : "Brush.Hairline"),
            BorderThickness = new Thickness(1),
        };
        AutomationProperties.SetName(card, run.AutomationName);
        card.Click += (_, _) => _page?.OpenRun(run);
        return card;
    }
}
