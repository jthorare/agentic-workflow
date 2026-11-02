using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Views.Workflows;

/// <summary>The run drawer: each button asks the view model, which asks the shell to open the run, copies text, or (Take over) sends the takeover.</summary>
internal sealed partial class RunInspectorView : UserControl
{
    public RunInspectorView()
    {
        InitializeComponent();
    }

    private RunInspectorViewModel? Inspector => DataContext as RunInspectorViewModel;

    private void OnClose(object? sender, RoutedEventArgs e) => Inspector?.Close();

    private void OnPrevious(object? sender, RoutedEventArgs e) => Inspector?.Previous();

    private void OnNext(object? sender, RoutedEventArgs e) => Inspector?.Next();

    private void OnCopyReference(object? sender, RoutedEventArgs e) => _ = Inspector?.CopyReference(CancellationToken.None);

    private void OnOpenRun(object? sender, RoutedEventArgs e) => Inspector?.OpenRun();

    private void OnTakeOver(object? sender, RoutedEventArgs e) => Inspector?.TakeOver();

    private void OnHandBack(object? sender, RoutedEventArgs e) => Inspector?.HandBack();

    private void OnCancel(object? sender, RoutedEventArgs e) => Inspector?.Cancel();

    private void OnReview(object? sender, RoutedEventArgs e) => _ = Inspector?.ReviewAndDecide(CancellationToken.None);

    private void OnTerminal(object? sender, RoutedEventArgs e) => _ = Inspector?.CopyTerminal(CancellationToken.None);
}
