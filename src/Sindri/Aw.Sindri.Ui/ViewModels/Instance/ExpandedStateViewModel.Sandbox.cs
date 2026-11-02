using Aw.Sindri.Ui.Api.Sandbox;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.ViewModels.Components;
using Aw.Sindri.Ui.ViewModels.Takeover;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>The expanded state's sandbox lines (<see cref="ExpandedStateViewModel"/>, continued; SB-6b): the wait for a session slot and the takeover banner.</summary>
internal sealed partial class ExpandedStateViewModel
{
    private WaitingForSlotView? _waiting;
    private bool _waitSaid;
    private bool _sandboxedRun;

    /// <summary>"Waiting for a free session slot (3 of 3 running)", then "Started"; empty when the run never waited.</summary>
    public string WaitingLine { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the waiting line shows.</summary>
    public bool HasWaiting => WaitingLine.Length > 0;

    /// <summary>The polite live region that says the wait once and then "Started".</summary>
    public LiveAnnouncer WaitingVoice { get; } = new(Labels.WaitingResting);

    /// <summary>The sandbox banner of a taken-over run.</summary>
    public TakeoverBannerViewModel TakeoverBanner { get; } = new();

    /// <summary>Says the wait the frame opened on, once; the view calls it when it is in the tree.</summary>
    internal void AnnounceWaiting()
    {
        if (_waiting is { } waiting && !_waitSaid)
        {
            _waitSaid = true;
            WaitingVoice.Say(Labels.WaitingForSlotAnnounce(waiting.Running, waiting.Cap));
        }
    }

    private void InitSandbox(CurrentState current)
    {
        _waiting = current.Status.Run?.WaitingForSlot;
        WaitingLine = _waiting is { } waiting ? Labels.WaitingForSlot(waiting.Running, waiting.Cap) : string.Empty;
        OnChanged(nameof(HasWaiting));
    }

    // A re-read: the count moves in place (said once, not again), and the wait ending says "Started" once.
    private void UpdateSandbox(CurrentState current)
    {
        var now = current.Status.Run?.WaitingForSlot;
        if (now is { } waiting)
        {
            WaitingLine = Labels.WaitingForSlot(waiting.Running, waiting.Cap);
            _waiting = now;
            AnnounceWaiting();
        }
        else if (_waiting is not null && current.Status.Run?.Session is not null)
        {
            (_waiting, _waitSaid, WaitingLine) = (null, false, Labels.SlotStarted);
            WaitingVoice.Say(Labels.SlotStarted);
        }
        else if (_waiting is not null)
        {
            // The wait ended without a session (the launch was cancelled or failed, or the run moved on): no "Started" for a Step that never ran.
            (_waiting, _waitSaid, WaitingLine) = (null, false, string.Empty);
        }

        OnChanged(nameof(HasWaiting));
        DrawBanner();
    }

    private void DrawBanner()
    {
        var status = _current.Status;
        TakeoverBanner.Update(_sandboxedRun, status.Paused is not null || string.Equals(status.Header.State, "taken-over", StringComparison.Ordinal));
    }
}
