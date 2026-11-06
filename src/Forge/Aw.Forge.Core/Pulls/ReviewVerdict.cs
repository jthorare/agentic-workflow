namespace Aw.Forge.Core.Pulls;

/// <summary>What a submitted review says.</summary>
public enum ReviewVerdict
{
    /// <summary>A comment (GitHub's COMMENT).</summary>
    Comment = 1,

    /// <summary>An approval (APPROVE).</summary>
    Approve = 2,

    /// <summary>A request for changes (REQUEST_CHANGES).</summary>
    RequestChanges = 3,
}
