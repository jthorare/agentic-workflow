using Avalonia.Automation;
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
    private SettingsViewModel? _settings;
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
        _settings?.PropertyChanged -= OnSettingsChanged;
        _settings = DataContext as SettingsViewModel;
        _settings?.PropertyChanged += OnSettingsChanged;
        _setup = _settings?.Setup;
        _setup?.RevealRequested += OnRevealRequested;
        _filterWasEmpty = true;
        if (Content is StackPanel page)
        {
            ReplaceChecklist(page);
        }
    }

    // A host without setup draws no checklist, and none of its controls.
    private void ReplaceChecklist(StackPanel page)
    {
        if (_checklist is not null)
        {
            _ = page.Children.Remove(_checklist);
            _checklist = null;
        }

        if (_setup is not null)
        {
            _checklist = new SetupChecklist { Name = "SetupChecklist", DataContext = _setup, IsVisible = _settings?.ShowsSetup ?? true };
            page.Children.Insert(3, _checklist);   // after the title, the filter box and its no-match line
        }
    }

    // The checklist is drawn in code, so its visibility follows the filter here.
    private void OnSettingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_settings is not null && string.Equals(e.PropertyName, nameof(SettingsViewModel.Filter), StringComparison.Ordinal))
        {
            var empty = _settings.Filter.Trim().Length == 0;
            if (_filterWasEmpty && !empty)
            {
                CollectShownText(_settings);   // before the Shows* changes are read: the filter matches the words on the page
            }

            _filterWasEmpty = empty;
        }

        if (_checklist is not null && _settings is not null && string.Equals(e.PropertyName, nameof(SettingsViewModel.ShowsSetup), StringComparison.Ordinal))
        {
            _checklist.IsVisible = _settings.ShowsSetup;
        }
    }

    // The text each section shows now: its labels, buttons, descriptions and accessible names (Browse, Reset, Copy, Read key ...).
    internal void CollectShownText(SettingsViewModel settings)
    {
        (SettingsSection Section, Control?[] Parts)[] map =
        [
            (SettingsSection.Setup, [_checklist]),
            (SettingsSection.Choices, [ModelChoiceSection, LabelChoiceSection, SurfaceChoiceSection]),
            (SettingsSection.Sandbox, [SandboxSettingsSlot]),
            (SettingsSection.Library, [LibrarySettingsSlot]),
            (SettingsSection.Connections, [ConnectionsSettingsSlot]),
            (SettingsSection.ThisApp, [ThisAppName, ThisAppCard]),
        ];
        foreach (var (section, parts) in map)
        {
            var words = parts.OfType<Control>().SelectMany(ShownWords);
            settings.SetShownText(section, string.Join(' ', section == SettingsSection.Setup ? words.Concat(StepWords(settings)) : words));
        }
    }

    // The words are read again each time the box goes from empty to text, and kept while it has text: rows inside a section raise changes on themselves, not on
    // the section's view model, so no change watcher would see them all (#3453).
    private bool _filterWasEmpty = true;

    // A collapsed step's content may not be in the visual tree, so the steps' own words are read from the view model too.
    private static IEnumerable<string> StepWords(SettingsViewModel settings) =>
        (settings.Setup?.Steps ?? []).SelectMany(static s => new[] { s.Title, s.Sub, s.DetailText, s.HintText, s.Name, s.Note, s.ProgressText, s.ErrorText, s.ChipWord, s.Repo?.FolderLabel, s.Repo?.BrowseText, s.Repo?.BodyText, s.Repo?.NoteText, s.Repo?.AddText })
            .OfType<string>().Where(static w => w.Length > 0);

    private static IEnumerable<string> ShownWords(Control root) =>
        root.GetSelfAndVisualDescendants().OfType<Control>().SelectMany(static c => new[] { (c as TextBlock)?.Text, (c as ContentControl)?.Content as string, AutomationProperties.GetName(c) })
            .OfType<string>().Where(static s => s.Length > 0);

    // Esc in the filter box clears it.
    private void OnFilterKey(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Escape && _settings is { Filter.Length: > 0 })
        {
            _settings.Filter = string.Empty;
            e.Handled = true;
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
