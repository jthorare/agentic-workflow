using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>
/// The stalled and rebuilding banners (UI spec §4.1): shown while the views' asOf says so, with the view's own
/// EmptyText sentence as the API serves it. Stalled carries Rebuild views (the catalog's RebuildViews row); rebuilding
/// is a progress banner. U1 reads them from GET /sindri/services, the list view API P2 serves.
/// </summary>
internal sealed class ViewsBanner : ObservableObject
{
    /// <summary>True while the views are stalled.</summary>
    public bool IsStalled { get; private set => Set(ref field, value); }

    /// <summary>True while the views rebuild.</summary>
    public bool IsRebuilding { get; private set => Set(ref field, value); }

    /// <summary>True when either banner shows.</summary>
    public bool IsShown { get; private set => Set(ref field, value); }

    /// <summary>The view's sentence for its state, as served.</summary>
    public string Text { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The sentence the banner draws: <see cref="Text"/>, without its "run `sindri views rebuild`" while the Rebuild views button is beside it (the button does that).</summary>
    public string ShownText => HasRebuild && Text.EndsWith(RebuildCommand, StringComparison.Ordinal) ? Text[..^RebuildCommand.Length] : Text;

    private const string RebuildCommand = ": run `sindri views rebuild`";

    /// <summary>The polite live region's text while the banner shows (its sentence; "Rebuilding views" for progress), else its resting text (never empty).</summary>
    public string Announcement { get; private set => Set(ref field, value); } = Labels.Sindri;

    /// <summary>True from the banner going away until trouble comes again: the recovery's live flag.</summary>
    public bool IsRecovered { get; private set => Set(ref field, value); }

    /// <summary>The recovery's live text, else its resting text (never empty).</summary>
    public string RecoveredAnnouncement { get; private set => Set(ref field, value); } = Labels.Sindri;

    /// <summary>The Rebuild views control (stalled only), or null when the catalog has no such row.</summary>
    public ControlViewModel? Rebuild { get; private set => Set(ref field, value); }

    /// <summary>True when the stalled banner carries its Rebuild views control.</summary>
    public bool HasRebuild { get; private set => Set(ref field, value); }

    /// <summary>Shows what the view says; null (no such route before P2, or it failed) hides both banners.</summary>
    /// <param name="view">The services view, or null.</param>
    /// <param name="rebuild">The catalog's RebuildViews control, or null.</param>
    internal void Show(ServiceList? view, ControlViewModel? rebuild)
    {
        var was = IsShown;
        IsStalled = view?.AsOf.Stalled == true;
        IsRebuilding = !IsStalled && view?.AsOf.Rebuilding == true;
        IsShown = IsStalled || IsRebuilding;
        Text = IsShown ? view?.Empty ?? string.Empty : string.Empty;
        Rebuild = IsStalled ? rebuild : null;
        HasRebuild = Rebuild is not null;
        OnChanged(nameof(ShownText));
        Announce(was);
    }

    // The flag (IsShown) has already changed; now the text. The rebuilding sentence carries a cursor that moves on every
    // read, so the live region says the state, not the number. Going away after trouble is the recovery, flag then text.
    private void Announce(bool was)
    {
        Announcement = IsStalled ? Text : IsRebuilding ? Labels.ViewsRebuilding : Labels.Sindri;
        if (IsShown)
        {
            IsRecovered = false;   // trouble again: the flag drops, then the text rests
            RecoveredAnnouncement = Labels.Sindri;
        }
        else if (was)
        {
            IsRecovered = true;
            RecoveredAnnouncement = Labels.ViewsRecovered;
        }
    }
}
