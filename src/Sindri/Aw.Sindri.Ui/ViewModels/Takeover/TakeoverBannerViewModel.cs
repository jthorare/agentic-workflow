using Aw.Sindri.Ui.ViewModels.Components;

namespace Aw.Sindri.Ui.ViewModels.Takeover;

/// <summary>The sandbox banner on a taken-over run (sandbox spec §7): shown while the owner has a sandboxed run, dismissible once, back on the next takeover.</summary>
internal sealed class TakeoverBannerViewModel : ObservableObject
{
    private bool _dismissed;

    /// <summary>The golden text.</summary>
    public string Text { get; } = Labels.SandboxTakeoverBanner;

    /// <summary>The Dismiss button's word.</summary>
    public string DismissText { get; } = Labels.DismissBanner;

    /// <summary>True while the banner shows.</summary>
    public bool IsVisible { get; private set => Set(ref field, value); }

    /// <summary>The polite live region that reads the banner once on appearance.</summary>
    public LiveAnnouncer Voice { get; } = new(Labels.BannerResting);

    /// <summary>Takes what is known of the run; the banner is read once when it appears, and a dismissal lasts until the run is handed back.</summary>
    /// <param name="sandboxed">True when the run's launch has a sandbox.</param>
    /// <param name="takenOver">True while the owner has the run.</param>
    internal void Update(bool sandboxed, bool takenOver)
    {
        if (!takenOver)
        {
            _dismissed = false;   // the next takeover brings it back
        }

        var show = sandboxed && takenOver && !_dismissed;
        if (show && !IsVisible)
        {
            Voice.Say(Text);
        }

        IsVisible = show;
    }

    /// <summary>Dismisses the banner until the next takeover.</summary>
    internal void Dismiss()
    {
        _dismissed = true;
        IsVisible = false;
        Voice.Quiet();
    }
}
