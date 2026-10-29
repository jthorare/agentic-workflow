using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.Projects;

namespace Aw.Sindri.Ui.Views.Projects;

/// <summary>The Projects page: it tells its view model its own width, which sets how many cards sit across; a card's click or Enter asks for its sheet, and a write that closed a sheet moves focus.</summary>
internal sealed partial class ProjectsPage : UserControl
{
    private ProjectsPageViewModel? _watched;

    public ProjectsPage()
    {
        InitializeComponent();
        SizeChanged += (_, e) => Resize(e.NewSize.Width);
        DataContextChanged += (_, _) =>
        {
            Resize(Bounds.Width);
            Watch();
        };
        AddHandler(ProjectCard.OpenEvent, OnOpen);
    }

    private void Resize(double width)
    {
        if (DataContext is ProjectsPageViewModel page && width > 0)
        {
            page.SetPageWidth(width);
        }
    }

    private void Watch()
    {
        if (_watched is { } old)
        {
            old.FocusRequested -= OnFocusRequested;
        }

        _watched = DataContext as ProjectsPageViewModel;
        if (_watched is { } now)
        {
            now.FocusRequested += OnFocusRequested;
        }
    }

    private void OnOpen(object? sender, RoutedEventArgs e)
    {
        if (e.Source is ProjectCard { DataContext: ProjectCardViewModel card } && DataContext is ProjectsPageViewModel page)
        {
            page.RequestOpen(card.Link);
        }
    }

    // After the list is read again and the sheet has closed: the next card's frame, the Add button, or a fold's button. Add project is
    // disabled until its form ships, so focus then goes to the page title rather than nowhere.
    private void OnFocusRequested(object? sender, ProjectFocusEventArgs e) =>
        Dispatcher.UIThread.Post(() => _ = Target(e)?.Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);

    private Control? Target(ProjectFocusEventArgs e) => e.Target switch
    {
        ProjectFocus.Card => this.GetVisualDescendants().OfType<ProjectCard>()
            .FirstOrDefault(c => c is { DataContext: ProjectCardViewModel card } && string.Equals(card.Id, e.CardId, StringComparison.Ordinal))?.FindControl<Border>("ProjectCardFrame"),
        ProjectFocus.HiddenFold => FoldButton("ProjectsHidden"),
        ProjectFocus.FinishedFold => FoldButton("ProjectsFinished"),
        ProjectFocus.Add => this.FindControl<Button>("ProjectsAdd") is { IsEnabled: true } add ? add : this.FindControl<Button>("ProjectsAddEmpty") is { IsEnabled: true, IsEffectivelyVisible: true } empty ? empty : this.FindControl<TextBlock>("ProjectsTitle"),
        _ => null,
    };

    private ToggleButton? FoldButton(string fold) => this.FindControl<Expander>(fold)?.GetVisualDescendants().OfType<ToggleButton>().FirstOrDefault();
}
