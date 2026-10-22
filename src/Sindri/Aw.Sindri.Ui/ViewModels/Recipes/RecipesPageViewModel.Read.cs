using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Recipes;

/// <summary>The Recipes page's reads (<see cref="RecipesPageViewModel"/>, continued): when it reads, and the keys that move through the rows.</summary>
internal sealed partial class RecipesPageViewModel
{
    /// <summary>The catch-up read started by the last show.</summary>
    internal Task CatchUp { get; private set; } = Task.CompletedTask;

    /// <summary>How many lists this page has drawn (a test waits on it).</summary>
    internal int Drawn { get; private set; }

    /// <summary>True while the page is the one shown.</summary>
    internal bool IsActive { get; private set; }

    /// <summary>Reads again after a failed first read.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the list is shown.</returns>
    internal Task Retry(CancellationToken cancellationToken) => _url is { } url ? Read(url, cancellationToken) : Task.CompletedTask;

    /// <summary>Re-reads the list (on connect and on every event). While the page is not the one shown it only remembers where the API is.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the list is shown.</returns>
    internal Task Refresh(Uri url, CancellationToken cancellationToken)
    {
        _url = url;
        return IsActive ? Read(url, cancellationToken) : Task.CompletedTask;
    }

    /// <summary>The page was shown or hidden (the shell's page changed). Becoming the page shown reads at once.</summary>
    /// <param name="shown">True while the Recipes page is the page.</param>
    internal void SetShown(bool shown)
    {
        var was = IsActive;
        IsActive = shown;
        if (shown && !was && _url is { } url)
        {
            _shown = new CancellationTokenSource();
            CatchUp = CatchUpRead(url, _shown.Token);
        }
        else if (!shown)
        {
            _reads++;   // a read already on its way is not wanted
            Dispose();
        }
    }

    // The read made when the page is shown, awaited by CatchUp so nothing about it is unobserved: a leave cancels it, any other failure is drawn as a failed read.
    private async Task CatchUpRead(Uri url, CancellationToken cancellationToken)
    {
        try
        {
            await Read(url, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // the page was left
        }
        catch (HttpRequestException e)
        {
            Fail(SindriUiErrors.Unreachable.With(e.Message));
        }
    }

    private async Task Read(Uri url, CancellationToken cancellationToken)
    {
        var read = ++_reads;
        var list = await api.Recipes(url, cancellationToken).ConfigureAwait(true);
        if (read != _reads)
        {
            return;   // a newer read was asked for since, or the page was left: its answer is the one to show
        }

        if (list.IsSuccess)
        {
            Show(list.Value);
            return;
        }

        Fail(list.Error);
    }

    /// <summary>One key: J and K move through the rows, Enter opens the selected one.</summary>
    /// <param name="key">The key.</param>
    /// <returns>True when the key did something.</returns>
    internal bool Press(string key) => key switch
    {
        "J" => Move(1),
        "K" => Move(-1),
        "Enter" => Selected is { } row && Opened(row),
        _ => false,
    };

    private bool Opened(RecipeRowViewModel row)
    {
        RequestOpen(row.Link);
        return true;
    }

    private bool Move(int by)
    {
        if (Rows.Count == 0)
        {
            return false;
        }

        var at = Selected is null ? -1 : Rows.IndexOf(Selected);
        Mark(Rows[Math.Clamp(at + by, 0, Rows.Count - 1)]);
        return true;
    }

    private void Mark(RecipeRowViewModel? row)
    {
        Selected?.IsSelected = false;
        Selected = row;
        Selected?.IsSelected = true;
    }
}
