namespace Aw.Forge.Core.Pulls;

/// <summary>What sort of conversation item a thread is.</summary>
public enum CommentKind
{
    /// <summary>An inline review thread.</summary>
    Thread = 1,

    /// <summary>A review's own body.</summary>
    Review = 2,

    /// <summary>A comment on the pull request's conversation.</summary>
    Comment = 3,
}
