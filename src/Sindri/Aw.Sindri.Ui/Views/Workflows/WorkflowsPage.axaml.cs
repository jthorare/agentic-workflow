using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Views.Workflows;

/// <summary>The Workflows page: a row's title opens its instance, and the filters narrow the list.</summary>
internal sealed partial class WorkflowsPage : UserControl
{
    public WorkflowsPage()
    {
        InitializeComponent();
        AddHandler(InstanceRow.OpenEvent, OnOpen);
        AddHandler(InstanceRow.SelectEvent, OnSelect);
        AddHandler(RecipeGroup.ToggleEvent, OnToggle);
        AddHandler(ChildAnswerButton.FollowEvent, OnChildAnswer);
    }

    private bool _focusWasHere;

    private void OnChildAnswer(object? sender, RoutedEventArgs e)
    {
        if (e.Source is ChildAnswerButton { Answer: { } answer } button && button.FindAncestorOfType<InstanceRow>() is { Address.Length: > 0 } row)
        {
            (DataContext as WorkflowsViewModel)?.FollowChildAnswer(row.Address, answer);
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        TopLevel.GetTopLevel(this)?.AddHandler(GotFocusEvent, OnFocusMoved, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        TopLevel.GetTopLevel(this)?.RemoveHandler(GotFocusEvent, OnFocusMoved);
        base.OnDetachedFromVisualTree(e);
    }

    // Where focus last went: a re-read only claims lost focus for the page when the human was last on it.
    private void OnFocusMoved(object? sender, RoutedEventArgs e) => _focusWasHere = e.Source is Visual source && this.IsVisualAncestorOf(source);

    private WorkflowsViewModel? List => DataContext as WorkflowsViewModel;

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        List?.FocusRequested -= OnFocusRequested;
        List?.SearchFocusRequested -= OnSearchFocusRequested;
        base.OnDataContextChanged(e);
        List?.FocusRequested += OnFocusRequested;
        List?.SearchFocusRequested += OnSearchFocusRequested;
    }

    // The "/" key: the search box takes focus.
    private void OnSearchFocusRequested(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() => _ = this.FindControl<TextBox>("SubjectFilter")?.Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);

    // A key moved the selection onto a row, or a re-read changed the rows under it: the row's frame takes focus, on the keyboard's ring, and
    // scrolls into view. A re-read only takes it when focus was lost with the old rows (a reorder drops the containers), never from a control
    // the human is on.
    private void OnFocusRequested(object? sender, WorkflowFocusEventArgs request) =>
        Dispatcher.UIThread.Post(() => FocusRow(request), DispatcherPriority.Loaded);

    private void FocusRow(WorkflowFocusEventArgs request)
    {
        var row = DrawnRow(request.Address);
        if (row is null || row.IsKeyboardFocusWithin || (request.OnlyIfLost && (HasUsableFocus() || !_focusWasHere)))
        {
            return;   // not drawn, or focus is already in the row (a click or its control), or a re-read must leave a usable focus alone, and a focus that was elsewhere (the sheet)
        }

        if (FrameOf(row) is { } frame)
        {
            _ = frame.Focus(NavigationMethod.Tab);
            frame.BringIntoView();
        }
    }

    // The list row or the board card drawn for an address.
    private Control? DrawnRow(string address) =>
        (Control?)this.GetVisualDescendants().OfType<InstanceRow>().FirstOrDefault(r => r.IsEffectivelyVisible && string.Equals(r.Address, address, StringComparison.Ordinal))
        ?? this.GetVisualDescendants().OfType<RunCard>().FirstOrDefault(r => r.IsEffectivelyVisible && string.Equals(r.Address, address, StringComparison.Ordinal));

    // The border that takes keyboard focus in a row or a card.
    private static Border? FrameOf(Control row) =>
        row.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => string.Equals(b.Name, "WorkflowRowFrame", StringComparison.Ordinal) || string.Equals(b.Name, "WorkflowCardFrame", StringComparison.Ordinal));

    // Focus on a control that is still in the tree, shown and enabled.
    private bool HasUsableFocus() =>
        TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is Control { IsEffectivelyVisible: true, IsEffectivelyEnabled: true } focused && TopLevel.GetTopLevel(focused) is not null;

    // Focus arriving on a row, or a control in it, selects it: the keys act on the row the human is on.
    private void OnSelect(object? sender, RoutedEventArgs e)
    {
        if (AddressOf(e.Source) is { Length: > 0 } address)
        {
            _ = List?.Select(address);
        }
    }

    // The run a list row or a board card stands for.
    private static string? AddressOf(object? source) => source switch
    {
        InstanceRow row => row.Address,
        RunCard card => card.Address,
        _ => null,
    };

    private void OnOpen(object? sender, RoutedEventArgs e)
    {
        if (AddressOf(e.Source) is { Length: > 0 } address)
        {
            List?.Open(address);
        }
    }

    private void OnToggle(object? sender, RoutedEventArgs e) => (e.Source as RecipeGroup)?.Group?.Toggle();

    private void OnRecipeChosen(object? sender, SelectionChangedEventArgs e)
    {
        if (List is { } list && sender is ComboBox { SelectedItem: string chosen })
        {
            var recipe = list.RecipeIdOf(chosen);   // a choice is a workflow's name; null for "All workflow types"
            if (!string.Equals(recipe, list.RecipeFilter, StringComparison.Ordinal))
            {
                _ = list.FilterRecipe(recipe, CancellationToken.None);
            }
        }
    }

    // The subject filter applies on Enter, so typing never re-reads the list under the human's hands.
    private void OnSubjectKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && List is { } list && sender is TextBox box)
        {
            _ = list.FilterSubject(box.Text, CancellationToken.None);
            e.Handled = true;
        }
    }

    // Typing narrows what is loaded as it goes; Enter (above) asks the API.
    private void OnSubjectTyped(object? sender, TextChangedEventArgs e) => List?.SetSearch((sender as TextBox)?.Text);

    private void OnStart(object? sender, RoutedEventArgs e) => List?.RequestStart();
}
