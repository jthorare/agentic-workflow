namespace Aw.Sindri.Core.Forge;

/// <summary>What a <see cref="PullThread"/> is.</summary>
public enum PullCommentKind
{
    /// <summary>An inline review thread, resolved or not.</summary>
    Thread = 1,

    /// <summary>A review's own body.</summary>
    Review = 2,

    /// <summary>A top-level comment on the pull request.</summary>
    Comment = 3,
}
