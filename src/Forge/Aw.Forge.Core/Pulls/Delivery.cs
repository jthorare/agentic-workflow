namespace Aw.Forge.Core.Pulls;

/// <summary>Whether a reply or reaction was made by this call.</summary>
public enum Delivery
{
    /// <summary>This call made it.</summary>
    Delivered = 1,

    /// <summary>The marker was already there; nothing was sent.</summary>
    AlreadyDelivered = 2,
}
