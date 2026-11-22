namespace Aw.Meta.Core.Delegations;

/// <summary>Where a delegation stands.</summary>
public enum DelegationState
{
    /// <summary>The ACTION message has not reached the lane yet: the meta owes a send.</summary>
    Owed = 1,

    /// <summary>The lane got the ACTION message and has not answered.</summary>
    Sent = 2,

    /// <summary>The lane sent a message naming the issue after it was delegated.</summary>
    Answered = 3,
}
