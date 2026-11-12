using Avalonia.Controls;
using Avalonia.Layout;

namespace Aw.Spike.T3335;

internal sealed class MainWindow : Window
{
    public const string Session = "s1";
    private readonly Border _host = new();
    private readonly ComboBox _mode = new() { ItemsSource = new[] { "a", "c" }, SelectedIndex = 0, Width = 90 };
    private IPane? _pane;

    public MainWindow(bool autoStart)
    {
        Title = "T3335 terminal spike";
        Width = 1000;
        Height = 640;
        var start = new Button { Content = "Start session + pane" };
        var detach = new Button { Content = "Close pane (detach)" };
        var kill = new Button { Content = "Kill session" };
        start.Click += async (_, _) => await ShowPaneAsync(_mode.SelectedItem as string ?? "a");
        detach.Click += async (_, _) => await ClosePaneAsync();
        kill.Click += async (_, _) =>
        {
            await ClosePaneAsync();
            await SpikeTmux.KillSessionAsync(Session);
        };
        var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new(8) };
        bar.Children.Add(_mode);
        bar.Children.Add(start);
        bar.Children.Add(detach);
        bar.Children.Add(kill);
        var root = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        root.Children.Add(bar);
        root.Children.Add(_host);
        Content = root;
        if (autoStart)
        {
            Opened += async (_, _) =>
            {
                _mode.SelectedItem = Environment.GetEnvironmentVariable("AW_SPIKE_T3335_MODE") is "c" ? "c" : "a";
                await ShowPaneAsync(_mode.SelectedItem as string ?? "a");
            };
        }
    }

    public Border Host => _host;

    public async Task<IPane> ShowPaneAsync(string mode, string command = "bash --norc")
    {
        await ClosePaneAsync();
        await SpikeTmux.NewSessionAsync(Session, 120, 30, command);
        IPane pane = mode switch
        {
            "c" => new PaneC(kick: true),
            "c-nokick" => new PaneC(kick: false),
            _ => new PaneA(),
        };
        _host.Child = pane.View;
        _pane = pane;
        await pane.StartAsync(Session);
        return pane;
    }

    public async Task ClosePaneAsync()
    {
        if (_pane is not null)
        {
            await _pane.CloseAsync();
            _host.Child = null;
            _pane = null;
        }
    }
}
