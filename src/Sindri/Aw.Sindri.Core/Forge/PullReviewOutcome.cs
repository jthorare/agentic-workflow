namespace Aw.Sindri.Core.Forge;

/// <summary>How a review post ended.</summary>
public enum PullReviewOutcome
{
    /// <summary>Posted now.</summary>
    Posted = 1,

    /// <summary>The same marker was posted before; nothing was posted again.</summary>
    AlreadyDelivered = 2,

    /// <summary>The head is not the one the review was written against. Nothing was sent.</summary>
    HeadMoved = 3,

    /// <summary>The forge refused a line comment. Nothing was posted.</summary>
    LineRejected = 4,
}
