namespace Aw.Sindri.Core.Forge;

/// <summary>Whether a reply or reaction was posted by this call.</summary>
public enum PullDelivery
{
    /// <summary>Posted now.</summary>
    Delivered = 1,

    /// <summary>The same marker was posted before; nothing was posted again.</summary>
    AlreadyDelivered = 2,
}
