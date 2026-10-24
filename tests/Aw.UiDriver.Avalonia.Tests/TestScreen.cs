using System.Globalization;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Threading;

namespace Aw.UiDriver.Avalonia.Tests;

/// <summary>The window the tests drive: a form with a button, a text box, a toggle, a disabled button, a scrolling list and a key listener.</summary>
internal sealed class TestScreen
{
    internal TestScreen()
    {
        Greeting = new TextBlock { Text = "Hello, Ada" };
        AutomationProperties.SetAutomationId(Greeting, "greeting");
        Save = new Button { Content = "Save", Command = Saved };
        AutomationProperties.SetAutomationId(Save, "save");
        Off = new Button { Content = "Off", IsEnabled = false };
        AutomationProperties.SetAutomationId(Off, "off");
        NameBox = new TextBox { Text = "Ada" };
        AutomationProperties.SetAutomationId(NameBox, "name");
        Dark = new ToggleButton { Content = "Dark" };
        AutomationProperties.SetAutomationId(Dark, "dark");
        Listener = new Border { Focusable = true, Width = 20, Height = 20 };
        AutomationProperties.SetAutomationId(Listener, "listener");
        Listener.KeyDown += (_, e) => Keys.Add($"{e.KeyModifiers}+{e.Key}");
        var rows = new StackPanel();
        for (var i = 0; i < 40; i++)
        {
            rows.Children.Add(new TextBlock { Text = "row " + i.ToString(CultureInfo.InvariantCulture), Height = 20 });
        }

        // No theme is loaded, so a scroll viewer has no template; this one is the smallest that scrolls: a content presenter holding the rows.
        List = new ScrollViewer
        {
            Height = 100,
            Template = new FuncControlTemplate<ScrollViewer>((_, scope) =>
            {
                var presenter = new ScrollContentPresenter { Name = "PART_ContentPresenter", Content = rows };
                scope.Register("PART_ContentPresenter", presenter);
                return presenter;
            }),
        };
        AutomationProperties.SetAutomationId(List, "list");
        var form = new StackPanel { Children = { Greeting, Save, Off, NameBox, Dark, Listener, List } };
        Window = new Window { Width = 400, Height = 300, Title = "Test screen", Content = form };
        Window.Show();
    }

    internal CountingCommand Saved { get; } = new();

    internal List<string> Keys { get; } = [];

    internal Window Window { get; }

    internal TextBlock Greeting { get; }

    internal Button Save { get; }

    internal Button Off { get; }

    internal TextBox NameBox { get; }

    internal ToggleButton Dark { get; }

    internal Border Listener { get; }

    internal ScrollViewer List { get; }

    /// <summary>Builds the screen on the UI thread (kept running by <see cref="UiPump"/>).</summary>
    /// <returns>The screen.</returns>
    internal static Task<TestScreen> OpenAsync() =>
        Dispatcher.UIThread.InvokeAsync(static () => new TestScreen(), DispatcherPriority.Normal, TestContext.Current.CancellationToken).GetTask();

    /// <summary>Runs work on the UI thread.</summary>
    /// <typeparam name="T">The result.</typeparam>
    /// <param name="work">The work.</param>
    /// <returns>Its result.</returns>
    internal static Task<T> OnUi<T>(Func<T> work) =>
        Dispatcher.UIThread.InvokeAsync(work, DispatcherPriority.Normal, TestContext.Current.CancellationToken).GetTask();
}
