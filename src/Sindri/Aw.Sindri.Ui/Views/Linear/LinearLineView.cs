using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Linear;
using AvaloniaRun = Avalonia.Controls.Documents.Run;

namespace Aw.Sindri.Ui.Views.Linear;

#pragma warning disable MA0182 // why: LinearIssueSheetView.axaml places it in a data template, which the analyzer cannot see
/// <summary>One line of an issue's description or a comment: its runs as text, a link run as a button that opens it in the browser (the run's text already names its host).</summary>
internal sealed class LinearLineView : SelectableTextBlock
{
    /// <summary>The line to draw.</summary>
    public static readonly StyledProperty<LinearTextLine?> LineProperty = AvaloniaProperty.Register<LinearLineView, LinearTextLine?>(nameof(Line));

    static LinearLineView()
    {
        _ = LineProperty.Changed.AddClassHandler<LinearLineView>((view, _) => view.Draw());
    }

    public LinearLineView()
    {
        TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        Classes.Add("body");
    }

    public LinearTextLine? Line
    {
        get => GetValue(LineProperty);
        set => SetValue(LineProperty, value);
    }

    private void Draw()
    {
        Inlines?.Clear();
        var line = Line;
        AutomationProperties.SetName(this, line?.Text ?? string.Empty);
        if (line is null)
        {
            return;
        }

        Inlines ??= [];
        foreach (var run in line.Runs)
        {
            if (!run.IsLink)
            {
                Inlines.Add(new AvaloniaRun(run.Text));
                continue;
            }

            var button = new HyperlinkButton { Padding = new Thickness(0), Content = new TextBlock { Text = run.Text, TextWrapping = Avalonia.Media.TextWrapping.Wrap }, Tag = run };
            AutomationProperties.SetName(button, run.Text);
            button.Click += OnOpen;
            Inlines.Add(new InlineUIContainer(button));
        }

        if (line.Runs.Count == 0)
        {
            Inlines.Add(new AvaloniaRun(" "));   // a blank line keeps its height
        }
    }

    private static void OnOpen(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: LinearTextRun run })
        {
            e.Handled = true;
            _ = run.Open(CancellationToken.None);
        }
    }
}
