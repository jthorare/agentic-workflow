using Aw.Sindri.Ui.ViewModels.Components;
using Aw.Sindri.Ui.ViewModels.Takeover;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>The expanded state's sandbox lines (<see cref="ExpandedStateViewModel"/>, continued; SB-6b): the wait for a session slot and the takeover banner.</summary>
internal sealed partial class ExpandedStateViewModel
{
    /// <summary>"Waiting for a free session slot (3 of 3 running)", then "Started"; empty when the run never waited.</summary>
    public string WaitingLine { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the waiting line shows.</summary>
    public bool HasWaiting => WaitingLine.Length > 0;

    /// <summary>The polite live region that says the wait once and then "Started".</summary>
    public LiveAnnouncer WaitingVoice { get; } = new(Labels.WaitingResting);

    /// <summary>The sandbox banner of a taken-over run.</summary>
    public TakeoverBannerViewModel TakeoverBanner { get; } = new();

    /// <summary>Says the wait the frame opened on, once; the view calls it when it is in the tree.</summary>
    internal void AnnounceWaiting() => OnChanged(nameof(WaitingLine));
}
