using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Settings;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The shell's local preferences (§3.2, §12.3): the window's bounds, the sheet's width and the last page.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>Gives the lists the text scale Settings holds (Settings calls it when it loads and on every change).</summary>
    /// <param name="appearance">The appearance Settings applied.</param>
    internal void ApplyAppearance(AppearanceSettings appearance)
    {
        NeedsYou.TextScale = appearance.TextScale;
        Strip.TextScale = appearance.TextScale;
        NeedsYou.Seen.SetInAppReduceMotion(appearance.ReduceMotion);
    }

    /// <summary>Remembers the sheet's width (clamped to 420–760 when it's next read).</summary>
    /// <param name="width">The width the user dragged it to.</param>
    internal void ResizeSheet(double width)
    {
        if (IsInstanceSheet || IsRecipeSheet)
        {
            return;   // an instance's or a recipe's sheet is always the widest; the width the human chose for the others stays
        }

        Remember(p => p with { SheetWidth = width });
        OnChanged(nameof(SheetWidth));
    }

    /// <summary>Remembers the window's bounds (grown to 900 × 600 when they're next read); bounds already remembered aren't written again.</summary>
    /// <param name="bounds">Where the window is.</param>
    internal void RememberWindow(WindowBounds bounds)
    {
        if ((_preferences with { Window = bounds }).Clamp().Window != _preferences.Window)
        {
            Remember(p => p with { Window = bounds });
        }
    }

    /// <summary>Reads the stored preferences: the last page, the window's bounds and the sheet's width (the defaults when they can't be read).</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when they are applied.</returns>
    internal async Task Restore(CancellationToken cancellationToken)
    {
        try
        {
            _preferences = await _store.Load(cancellationToken).ConfigureAwait(true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _preferences = UiPreferences.Defaults;
        }

        CurrentPage = Enum.TryParse<PageId>(_preferences.LastPage, out var page) ? page : PageId.Home;
        Queue.PageShown(RowScopeOf(CurrentPage));
        OpenPage(CurrentPage);   // a restored page was not navigated to, but it is entered all the same
        await NeedsYou.Restore(cancellationToken).ConfigureAwait(true);
        Changed(_pageProperties);
        Changed(nameof(Window), nameof(SheetWidth));
    }

    // The palette's history (§12.3 paletteHistory): an entry run moves to the newest end.
    private void RememberRun(string entry) =>
        Remember(p => p with { PaletteHistory = [.. p.PaletteHistory.Where(e => !string.Equals(e, entry, StringComparison.Ordinal)), entry] });

    // The shell's fields are written over whatever the file holds by then, through the app's one write queue (shared with
    // Settings, so neither writes back what it read before the other's change).
    private void Remember(Func<UiPreferences, UiPreferences> change)
    {
        _preferences = change(_preferences).Clamp();
        _ = _store.Update(change);
    }
}
