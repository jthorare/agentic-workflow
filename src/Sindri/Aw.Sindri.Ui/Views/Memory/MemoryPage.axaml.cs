using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Memory;

namespace Aw.Sindri.Ui.Views.Memory;

#pragma warning disable MA0182 // why: ShellWindow.axaml places it in a Deferred template, which the analyzer cannot see
/// <summary>The Memory page: each button sends one API call through its row's view model and shows the answer.</summary>
internal sealed partial class MemoryPage : UserControl
{
    public MemoryPage()
    {
        InitializeComponent();
    }

    private static T? Context<T>(object? sender)
        where T : class => (sender as StyledElement)?.DataContext as T;

#pragma warning disable MA0155 // why: an Avalonia RoutedEvent handler returns void, so it can only be async void
    private async void OnRetry(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MemoryPageViewModel page)
        {
            await page.Retry(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnCreate(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MemoryPageViewModel page)
        {
            await page.CreateStore(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private void OnToggle(object? sender, RoutedEventArgs e)
    {
        if (Context<MemoryStoreRowViewModel>(sender) is { } store)
        {
            store.IsOpen = !store.IsOpen;
        }
    }

    private async void OnSearch(object? sender, RoutedEventArgs e)
    {
        if (Context<MemorySearchViewModel>(sender) is { } search)
        {
            await search.Run(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnSearchKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Context<MemorySearchViewModel>(sender) is { } search)
        {
            e.Handled = true;
            await search.Run(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnWrite(object? sender, RoutedEventArgs e)
    {
        if (Context<MemoryStoreRowViewModel>(sender) is { } store)
        {
            await store.Write(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnForgetFact(object? sender, RoutedEventArgs e)
    {
        if (Context<MemoryFactRowViewModel>(sender) is { } fact)
        {
            await fact.Forget(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnForgetTag(object? sender, RoutedEventArgs e)
    {
        if (Context<MemoryTagViewModel>(sender) is { } tag)
        {
            await tag.Forget(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnForgetExpired(object? sender, RoutedEventArgs e)
    {
        if (Context<MemoryStoreRowViewModel>(sender) is { } store)
        {
            await store.ForgetExpired(CancellationToken.None).ConfigureAwait(true);
        }
    }
#pragma warning restore MA0155
}
#pragma warning restore MA0182
