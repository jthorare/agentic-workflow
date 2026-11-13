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
        if (Content is StackPanel page)
        {
            ReplaceChecklist(page);
            PlaceThisApp(page);
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
            CollectShownText(_settings);   // before the Shows* changes are read: the filter matches the words on the page, read again on every change (#3476)
        }

        if (_checklist is not null && _settings is not null && string.Equals(e.PropertyName, nameof(SettingsViewModel.ShowsSetup), StringComparison.Ordinal))
        {
            _checklist.IsVisible = _settings.ShowsSetup;
        }

        if (Content is StackPanel page && _settings is not null && string.Equals(e.PropertyName, nameof(SettingsViewModel.ThisAppFirst), StringComparison.Ordinal) && _settings.ThisAppFirst != _placedFirst)
        {
            PlaceThisApp(page);   // only when the order really changes
        }
    }

    // The Reset button hides itself when it resets, so focus goes to the control it reset.
    private void OnResetMotion(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _settings?.ResetReduceMotion();
        _ = ReduceMotionSwitch.Focus();
    }

    private void OnResetTextSize(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _settings?.ResetTextScale();
        _ = TextScaleSlider.Focus();
    }

    // "This app" sits above the checklist once setup is finished (or when the page has none), and below the other sections before that.
    private bool _placedFirst;

    private void PlaceThisApp(StackPanel page)
    {
        Control[] parts = [ThisAppName, ThisAppCard];
        _placedFirst = _settings?.ThisAppFirst == true;
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;   // moving the parts drops focus from inside them
        var wasInside = focused is not null && parts.Any(p => p.IsVisualAncestorOf(focused));
        foreach (var part in parts)
        {
            _ = page.Children.Remove(part);
        }

        if (_settings?.ThisAppFirst == true)
        {
            page.Children.InsertRange(3, parts);   // after the title, the filter box and its no-match line, before the checklist
        }
        else
        {
            page.Children.AddRange(parts);
        }

        if (wasInside)
        {
            Dispatcher.UIThread.Post(() => focused?.Focus(), DispatcherPriority.Loaded);
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

    // A collapsed step's content may not be in the visual tree, so the steps' own words are read from the view model too.
    private static IEnumerable<string> StepWords(SettingsViewModel settings) =>
        (settings.Setup?.Steps ?? []).SelectMany(static s => new[] { s.Title, s.Sub, s.DetailText, s.HintText, s.Name, s.Note, s.ProgressText, s.ErrorText, s.ChipWord, s.Repo?.FolderLabel, s.Repo?.BrowseText, s.Repo?.BodyText, s.Repo?.NoteText, s.Repo?.AddText })
            .OfType<string>().Where(static w => w.Length > 0);

    // The words of a section as shown: a control hidden by its own binding (the "Changed" mark, "Reset" while nothing changed) adds none. The root itself may be hidden by the filter.
    private static IEnumerable<string> ShownWords(Control root) => ShownWords(root, isRoot: true);

    private static IEnumerable<string> ShownWords(Control control, bool isRoot)
    {
        if (!isRoot && !control.IsVisible)
        {
            yield break;
        }

        foreach (var word in new[] { (control as TextBlock)?.Text, (control as ContentControl)?.Content as string, AutomationProperties.GetName(control) }.OfType<string>().Where(static s => s.Length > 0))
        {
            yield return word;
        }

        foreach (var child in control.GetVisualChildren().OfType<Control>())
        {
            foreach (var word in ShownWords(child, isRoot: false))
            {
                yield return word;
            }
        }
    }

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
