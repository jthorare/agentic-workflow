using Aw.Sindri.Ui.ViewModels.Takeover;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The run drawer's take over (<see cref="RunInspectorViewModel"/>, continued): it sends the takeover and says what came of it.</summary>
internal sealed partial class RunInspectorViewModel
{
    private TakeoverSender? _takeover;

    /// <summary>What the last Take over came to, in words ("Sending…", the viewer's outcome, or the refusal with its code); empty before one.</summary>
    public string TakeOverLine { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The attach command to copy when no terminal opened; empty otherwise.</summary>
    public string TakeOverCommand { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when there is a take-over line to show.</summary>
    public bool HasTakeOverLine => TakeOverLine.Length > 0;

    /// <summary>True when there is an attach command to show.</summary>
    public bool HasTakeOverCommand => TakeOverCommand.Length > 0;

    /// <summary>Take over: sends "ApplyCommand:takeover" for the run, once per press, and shows the viewer's outcome or the refusal.</summary>
    internal void TakeOver()
    {
        if (Row is { } row)
        {
            var sender = _takeover ??= new TakeoverSender(api, () => _url);
            _ = sender.Press(row.InstanceId, _detail?.Current.Status.Run?.Launch, Say);
        }
    }

    private void Say(TakeoverView view)
    {
        (TakeOverLine, TakeOverCommand) = (view.Line, view.Command);
        OnChanged(nameof(HasTakeOverLine));
        OnChanged(nameof(HasTakeOverCommand));
    }
}
