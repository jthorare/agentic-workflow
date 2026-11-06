namespace Aw.Meta.Features.LaneSend;

/// <summary>The LaneSend request: the meta sends a message to a non-Claude lane.</summary>
/// <param name="Lane">The lane, as named in the team config.</param>
/// <param name="Text">The message text.</param>
public sealed record LaneSendRequest(string Lane, string Text)
{
    /// <summary>How long one wait for the woken lane's hook lasts (AW_META_WAKE_CONFIRM_SECONDS, default 10); the wake waits twice.</summary>
    public int WakeConfirmSeconds { get; init; } = 10;
}
