using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Linear;

namespace Aw.Sindri.Ui.Views.Linear;

/// <summary>The Linear "New issue" dialog; Enter in the title creates, Esc, Cancel and the scrim close it with the draft kept.</summary>
internal sealed partial class NewLinearIssueDialog : UserControl
{
    private NewLinearIssueViewModel? _form;

    public NewLinearIssueDialog()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_form is { } old)
        {
            old.PropertyChanged -= OnForm;
        }

        _form = DataContext as NewLinearIssueViewModel;
        if (_form is { } form)
        {
            form.PropertyChanged += OnForm;
        }

        base.OnDataContextChanged(e);
    }

    /// <summary>Moves focus into the team picker (the dialog just opened).</summary>
    internal void FocusTeam() => this.Get<ComboBox>("TeamField").Focus(NavigationMethod.Tab);

    /// <summary>Moves focus into the title field.</summary>
    internal void FocusTitle() => this.Get<TextBox>("TitleField").Focus(NavigationMethod.Tab);

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (DataContext is NewLinearIssueViewModel form && e.Key == Key.Enter && e.Source is TextBox { AcceptsReturn: false })
        {
            _ = form.IsUnknown ? form.Retry(CancellationToken.None) : form.Submit(CancellationToken.None);
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    // A blank submit asks for focus on the title, a submit with no team on the team, wherever the press came from.
    private void OnForm(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(NewLinearIssueViewModel.TitleFocusRequests), StringComparison.Ordinal))
        {
            FocusTitle();
        }
        else if (string.Equals(e.PropertyName, nameof(NewLinearIssueViewModel.TeamFocusRequests), StringComparison.Ordinal))
        {
            FocusTeam();
        }
    }

    private void OnCreate(object? sender, RoutedEventArgs e) => _ = (DataContext as NewLinearIssueViewModel)?.Submit(CancellationToken.None);

    private void OnRetry(object? sender, RoutedEventArgs e) => _ = (DataContext as NewLinearIssueViewModel)?.Retry(CancellationToken.None);

    private void OnDiscard(object? sender, RoutedEventArgs e) => (DataContext as NewLinearIssueViewModel)?.Discard();

    private void OnCancel(object? sender, RoutedEventArgs e) => (DataContext as NewLinearIssueViewModel)?.Close();

    private void OnScrim(object? sender, PointerPressedEventArgs e) => (DataContext as NewLinearIssueViewModel)?.Close();
}
