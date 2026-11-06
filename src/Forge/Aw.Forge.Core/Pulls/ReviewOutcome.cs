namespace Aw.Forge.Core.Pulls;

/// <summary>How a review submission ended.</summary>
public enum ReviewOutcome
{
    /// <summary>This call posted the review.</summary>
    Posted = 1,

    /// <summary>A review with the same marker was posted before; nothing was sent.</summary>
    AlreadyDelivered = 2,

    /// <summary>The head is not the expected one. Nothing was sent.</summary>
    HeadMoved = 3,

    /// <summary>The forge refused a line comment (the line is not in the diff it has). Nothing was posted.</summary>
    LineRejected = 4,
}
