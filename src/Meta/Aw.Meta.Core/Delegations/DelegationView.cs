namespace Aw.Meta.Core.Delegations;

/// <summary>A delegation as `aw-meta delegate` and `aw-meta delegated` print it.</summary>
/// <param name="Issue">The issue number.</param>
/// <param name="Url">The issue link.</param>
/// <param name="Title">The issue title.</param>
/// <param name="Lane">The lane.</param>
/// <param name="Session">The lane's session name, the SendMessage target for a Claude lane.</param>
/// <param name="At">When it was delegated.</param>
/// <param name="State">"owed", "sent" or "answered".</param>
/// <param name="Message">The ACTION message text.</param>
public sealed record DelegationView(int Issue, string Url, string Title, string Lane, string Session, DateTimeOffset At, string State, string Message)
{
    /// <summary>The view of a stored delegation in a given state.</summary>
    /// <param name="delegation">The delegation.</param>
    /// <param name="state">Its state.</param>
    /// <returns>The view.</returns>
    public static DelegationView Of(Delegation delegation, DelegationState state)
    {
        ArgumentNullException.ThrowIfNull(delegation);
        return new(delegation.Issue, delegation.Url, delegation.Title, delegation.Lane, delegation.Session, delegation.At, Name(state), delegation.Message);
    }

    /// <summary>The state's name in output: "owed", "sent" or "answered".</summary>
    /// <param name="state">The state.</param>
    /// <returns>The name.</returns>
    public static string Name(DelegationState state) => state switch
    {
        DelegationState.Owed => "owed",
        DelegationState.Sent => "sent",
        DelegationState.Answered => "answered",
        _ => "owed",
    };
}
