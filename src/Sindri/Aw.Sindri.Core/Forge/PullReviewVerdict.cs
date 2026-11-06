namespace Aw.Sindri.Core.Forge;

/// <summary>What a review says.</summary>
public enum PullReviewVerdict
{
    /// <summary>A comment.</summary>
    Comment = 1,

    /// <summary>An approval.</summary>
    Approve = 2,

    /// <summary>A request for changes.</summary>
    RequestChanges = 3,
}
