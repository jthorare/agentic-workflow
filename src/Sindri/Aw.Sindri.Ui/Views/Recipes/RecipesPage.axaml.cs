using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Recipes;
using Aw.Sindri.Ui.Views.Layout;

namespace Aw.Sindri.Ui.Views.Recipes;

/// <summary>The Recipes page: a title click on a row opens the workflow's sheet; the filters, the retry and Approve new workflows go to the view model.</summary>
internal sealed partial class RecipesPage : UserControl
{
    public RecipesPage()
    {
        InitializeComponent();
        AddHandler(RecipeRow.OpenEvent, OnOpen);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PageLayout.TierProperty && this.FindControl<Grid>("RecipesHeader") is { } header)
        {
            RecipeColumns.Apply(header, change.GetNewValue<PageTier>());
        }
    }

    private RecipesPageViewModel? Page => DataContext as RecipesPageViewModel;

#pragma warning disable MA0155 // why: an Avalonia RoutedEvent handler returns void, so it can only be async void
    private async void OnRetry(object? sender, RoutedEventArgs e)
    {
        if (Page is { } page)
        {
            await page.Retry(CancellationToken.None).ConfigureAwait(true);
        }
    }
#pragma warning restore MA0155

    private void OnAll(object? sender, RoutedEventArgs e) => Page?.ShowAll();

    private void OnNeeds(object? sender, RoutedEventArgs e) => Page?.ShowNeeds();

#pragma warning disable MA0155 // why: an Avalonia RoutedEvent handler returns void, so it can only be async void
    private async void OnWindow(object? sender, RoutedEventArgs e)
    {
        if (Page is { } page && sender is Control { DataContext: WindowChoiceViewModel choice })
        {
            await page.ChooseWindow(choice.Window, CancellationToken.None).ConfigureAwait(true);
        }
    }
#pragma warning restore MA0155

    private void OnApproveNew(object? sender, RoutedEventArgs e) => Page?.RequestApproveNew();

    private void OnOpen(object? sender, RoutedEventArgs e)
    {
        if (e.Source is RecipeRow { Recipe: { } recipe } && Page is { } page)
        {
            page.RequestOpen(recipe.Link);
        }
    }
}
