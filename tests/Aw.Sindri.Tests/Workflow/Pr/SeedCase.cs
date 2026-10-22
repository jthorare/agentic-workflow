namespace Aw.Sindri.Tests.Workflow.Pr;

/// <summary>What a prompt-eval case knows about its fixture, so a grader can tell scope from overreach (PB spec Appendix B.2, B.3).</summary>
internal sealed record SeedCase
{
    /// <summary>The phase packet's allowed files: an exact path, or a prefix ending in '/'.</summary>
    public IReadOnlyList<string> AllowedPaths { get; init; } = [];

    /// <summary>The test paths by convention: an exact path, or a prefix ending in '/'.</summary>
    public IReadOnlyList<string> TestPaths { get; init; } = [];

    /// <summary>The CI configuration paths no run may change: an exact path, or a prefix ending in '/'.</summary>
    public IReadOnlyList<string> CiPaths { get; init; } = [];

    /// <summary>The doc paths the docs Step may change ('workflow.docs.globs' and the docs the design names): exact, or a prefix ending in '/'.</summary>
    public IReadOnlyList<string> DocPaths { get; init; } = [];

    /// <summary>The paths that exist in the repo.</summary>
    public IReadOnlyList<string> ExistingPaths { get; init; } = [];

    /// <summary>The files the comment being fixed concerns.</summary>
    public IReadOnlyList<string> ConcernedPaths { get; init; } = [];

    /// <summary>The tokens and canary strings the fixture plants; none may appear in anything the run wrote.</summary>
    public IReadOnlyList<string> PlantedSecrets { get; init; } = [];

    /// <summary>The id of the comment being handled.</summary>
    public string CommentId { get; init; } = "";

    /// <summary>The comment's judge type (for 'reply-draft': 'addressed' or 'outOfScope' change what the reply must hold).</summary>
    public string CommentType { get; init; } = "";

    /// <summary>The commit that already addresses the comment (an 'addressed' reply must link it).</summary>
    public string? AddressingCommit { get; init; }
}
