using Aw.Kernel;
using Aw.Sindri.Ui.Api.Sandbox;
using Aw.Sindri.Ui.ViewModels.Components;

namespace Aw.Sindri.Ui.ViewModels.Item;

/// <summary>
/// One blocked target on the item sheet, with its fix (states.json 'blockedList', 'allow'). Allow is sent once per press; the button stays
/// enabled and focused through every state, and a press while sending does nothing. The list's one live region says each step.
/// </summary>
internal sealed class BlockedRowViewModel : ObservableObject
{
    private const string Network = "network";

    private readonly Func<AllowRequest, CancellationToken, Task<Result<AllowView>>> _send;
    private readonly LiveAnnouncer _voice;
    private readonly Action<ApprovalRequestedEventArgs> _approval;
    private BlockedTarget _target;
    private string _approvalId = string.Empty;

    internal BlockedRowViewModel(BlockedTarget target, Func<AllowRequest, CancellationToken, Task<Result<AllowView>>> send, LiveAnnouncer voice, Action<ApprovalRequestedEventArgs> approval)
    {
        (_target, _send, _voice, _approval) = (target, send, voice, approval);
        Draw();
    }

    /// <summary>"Blocked: host0.example.org × 10".</summary>
    public string Line { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The row by kind, with its fix.</summary>
    public string Text { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the row has an Allow button.</summary>
    public bool HasAllow => string.Equals(_target.Fix, "allow", StringComparison.Ordinal);

    /// <summary>The button's visible word: Allow, then Allowed or Refused.</summary>
    public string AllowText => State switch
    {
        AllowState.Allowed => Labels.AllowDone,
        AllowState.Refused => Labels.AllowRefused,
        AllowState.Idle or AllowState.Sending or AllowState.NeedsApproval => Labels.AllowButton,
        _ => throw new InvalidOperationException("an Allow state the row doesn't word"),
    };

    /// <summary>The button's accessible name.</summary>
    public string AllowName => AllowText + " " + _target.Target;

    /// <summary>Where Allow stands.</summary>
    public AllowState State
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(AllowText));
            OnChanged(nameof(AllowName));
        }
    }

    /// <summary>The server's reason for a refusal, as served; empty otherwise.</summary>
    public string Refusal { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The target's key across reads.</summary>
    internal string Key => _target.Key;

    /// <summary>Takes the counts of a new read.</summary>
    /// <param name="target">The target as read now.</param>
    internal void Update(BlockedTarget target)
    {
        _target = target;
        Draw();
    }

    /// <summary>The owner approved the profile: the allowance is in force.</summary>
    internal void Settle()
    {
        if (State == AllowState.NeedsApproval)
        {
            State = AllowState.Allowed;
            _voice.Say(Labels.AllowDone);
        }
    }

    /// <summary>True while the approval is open and not yet answered.</summary>
    internal bool IsWaitingForApproval => State == AllowState.NeedsApproval;

    /// <summary>Presses Allow; a second press while sending does nothing, and one while the approval is open opens it again.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that ends when the answer was drawn.</returns>
    internal async Task Allow(CancellationToken cancellationToken)
    {
        if (!HasAllow || State is AllowState.Sending or AllowState.Allowed)
        {
            return;
        }

        if (State == AllowState.NeedsApproval)
        {
            _voice.Say(Labels.AllowNeedsApproval(_target.Target));
            _approval(new ApprovalRequestedEventArgs(_approvalId, _target.Target));
            return;
        }

        (State, Refusal) = (AllowState.Sending, string.Empty);
        _voice.Say(Labels.AllowSending);
        var answer = await _send(new AllowRequest(_target.Launch, string.Equals(_target.Kind, Network, StringComparison.Ordinal) ? Network : "path", _target.Target), cancellationToken).ConfigureAwait(true);
        if (answer.IsError)
        {
            (State, Refusal) = (AllowState.Refused, answer.Error.Message);
            _voice.Say(Labels.AllowRefused);
            return;
        }

        (_approvalId, State) = (answer.Value.Approval, AllowState.NeedsApproval);
        _voice.Say(Labels.AllowNeedsApproval(_target.Target));
        _approval(new ApprovalRequestedEventArgs(_approvalId, _target.Target));
    }

    private void Draw()
    {
        Line = Labels.BlockedSome(_target.Target, _target.Count);
        Text = _target.Fix switch
        {
            "allow" when string.Equals(_target.Kind, Network, StringComparison.Ordinal) => Labels.BlockedHost(_target.Target, _target.Count),
            "allow" => Labels.BlockedPath(_target.Target, _target.Count),
            "never" => Labels.BlockedPathPermanent(_target.Target, _target.Reason),
            _ => Labels.BlockedPolicy(_target.Target, _target.Count),
        };
        OnChanged(nameof(HasAllow));
        OnChanged(nameof(AllowName));
    }
}
