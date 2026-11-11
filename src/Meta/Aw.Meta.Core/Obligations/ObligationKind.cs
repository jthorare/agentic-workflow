namespace Aw.Meta.Core.Obligations;

/// <summary>What the meta owes.</summary>
public enum ObligationKind
{
    /// <summary>A reply to an inbound message.</summary>
    Reply = 1,

    /// <summary>Retired: sending the lane map to every lane. Lanes read the lane map version from the board; nothing opens this any more.</summary>
    BroadcastLanes = 2,

    /// <summary>Retired: telling every lane main moved. Lanes read it from the board; nothing opens this any more.</summary>
    AnnounceMain = 3,

    /// <summary>Sending an owner's answer back to the lane that asked.</summary>
    RelayOwner = 4,

    /// <summary>Re-arming the watch.</summary>
    RearmWatch = 5,

    /// <summary>Recycling the meta lane itself with a detached lane-recycle, owed while its context is at or over the recycle mark and no waiter is recorded.</summary>
    RecycleSelf = 6,
}
