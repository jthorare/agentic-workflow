using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Agents;

namespace Aw.Sindri.Ui.Views.Agents;

#pragma warning disable MA0182 // why: the shell places it in a sheet template once A7's route and the page wiring land
/// <summary>The New agent form inside the shell's sheet: Ctrl+Enter starts, Ctrl+Shift+Enter starts in the background and Esc closes, from any field.</summary>
internal sealed partial class NewAgentFormView : UserControl
{
    public NewAgentFormView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
        DataContextChanged += (_, _) =>
        {
            if (DataContext is NewAgentFormViewModel form)
            {
                form.FocusRequested += (_, e) => FocusField(e.Field);
                form.PropertyChanged += (_, e) =>
                {
                    if (string.Equals(e.PropertyName, nameof(NewAgentFormViewModel.IsOpen), StringComparison.Ordinal) && form.IsOpen)
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() => FocusField(NewAgentField.Prompt), Avalonia.Threading.DispatcherPriority.Loaded);
                    }
                };
            }
        };
    }

    private void FocusField(NewAgentField field) =>
        (field switch
        {
            NewAgentField.Prompt => (Control?)this.FindControl<TextBox>("NewAgentPrompt"),
            NewAgentField.Repo => this.FindControl<ComboBox>("NewAgentRepo"),
            NewAgentField.Agent => this.FindControl<ComboBox>("NewAgentAgent"),
            NewAgentField.Worktree => this.FindControl<ToggleSwitch>("NewAgentWorktree"),
            NewAgentField.Start => this.FindControl<Button>("NewAgentStart"),
            _ => null,
        })?.Focus();

    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (DataContext is not NewAgentFormViewModel form)
        {
            return;
        }

        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var key = e.Key == Key.Enter ? "Enter" : e.Key == Key.Escape ? "Escape" : null;
        if (key is null || (key == "Enter" && !ctrl))
        {
            return;
        }

        e.Handled = true;
        _ = form.Press(key, ctrl, shift, CancellationToken.None);
    }

    private void OnStart(object? sender, RoutedEventArgs e) => Run(f => f.Start(background: false, CancellationToken.None));

    private void OnRetry(object? sender, RoutedEventArgs e) => Run(f => f.Retry(CancellationToken.None));

    private void OnCopyCommand(object? sender, RoutedEventArgs e) => Run(f => f.CopySignInCommand(CancellationToken.None));

    private void OnShowIt(object? sender, RoutedEventArgs e) => (DataContext as NewAgentFormViewModel)?.ShowIt();

    private void Run(Func<NewAgentFormViewModel, Task> action)
    {
        if (DataContext is NewAgentFormViewModel form)
        {
            _ = action(form);
        }
    }
}
#pragma warning restore MA0182
