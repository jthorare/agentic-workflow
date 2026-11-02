using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.Settings;
using Aw.Sindri.Ui.ViewModels.Setup;
using Aw.Sindri.Ui.Views.Setup;

namespace Aw.Sindri.Ui.Views;

/// <summary>The Settings page: binds to the settings view model and holds no logic beyond bringing the setup checklist into view when it is asked for.</summary>
internal sealed partial class SettingsView : UserControl
{
    private SetupViewModel? _setup;
    private SetupChecklist? _checklist;

    public SettingsView()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _setup?.RevealRequested -= OnRevealRequested;
        _setup = (DataContext as SettingsViewModel)?.Setup;
        _setup?.RevealRequested += OnRevealRequested;
        if (Content is StackPanel page)
        {
            if (_checklist is not null)
            {
                _ = page.Children.Remove(_checklist);
                _checklist = null;
            }

            if (_setup is not null)
            {
                _checklist = new SetupChecklist { Name = "SetupChecklist", DataContext = _setup };   // a host without setup draws no checklist, and none of its controls
                page.Children.Insert(1, _checklist);
            }
        }
    }

    // Opened by a link, G U, ⌘, or Home's line: the checklist is the first section, so it is brought to the top of the page.
    private void OnCopyModelChoice(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel { ModelChoice: { } line })
        {
            _ = line.Copy(CancellationToken.None);
        }
    }

    private void OnRevealRequested(object? sender, SetupRevealEventArgs e) =>
        Dispatcher.UIThread.Post(() => ((Control?)StepRow(e.StepId) ?? _checklist)?.BringIntoView(), DispatcherPriority.Loaded);

    private SetupStepRow? StepRow(string? id) =>
        id is null ? null : _checklist?.GetVisualDescendants().OfType<SetupStepRow>().FirstOrDefault(r => r.DataContext is SetupStepViewModel step && string.Equals(step.Id, id, StringComparison.Ordinal));
}
