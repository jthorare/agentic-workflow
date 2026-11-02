using System.Globalization;
using Aw.Connectors.Core.GitHub;

namespace Aw.Connectors.Testing.GitHub;

/// <summary>Builds a <see cref="GitHubIssue"/> with deterministic defaults (see <see cref="A.GitHubIssue"/>).</summary>
public sealed class GitHubIssueBuilder
{
    private readonly long _id;
    private DateTimeOffset _updatedAt = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private bool _pull;
    private string _body = "Steps to reproduce.";
    private string _state = "open";
    private int _comments;

    internal GitHubIssueBuilder(long id)
    {
        _id = id;
    }

    /// <summary>Sets the change time.</summary>
    /// <param name="updatedAt">When it last changed.</param>
    /// <returns>This builder.</returns>
    public GitHubIssueBuilder UpdatedAt(DateTimeOffset updatedAt)
    {
        _updatedAt = updatedAt;
        return this;
    }

    /// <summary>Makes it a pull request.</summary>
    /// <returns>This builder.</returns>
    public GitHubIssueBuilder AsPullRequest()
    {
        _pull = true;
        return this;
    }

    /// <summary>Sets the body.</summary>
    /// <param name="body">The markdown.</param>
    /// <returns>This builder.</returns>
    public GitHubIssueBuilder WithBody(string body)
    {
        _body = body;
        return this;
    }

    /// <summary>Sets the comment count the list reports (the comments themselves are added to the fake).</summary>
    /// <param name="comments">The count.</param>
    /// <returns>This builder.</returns>
    public GitHubIssueBuilder WithComments(int comments)
    {
        _comments = comments;
        return this;
    }

    /// <summary>Marks it closed.</summary>
    /// <returns>This builder.</returns>
    public GitHubIssueBuilder Closed()
    {
        _state = "closed";
        return this;
    }

    /// <summary>Builds the entry.</summary>
    /// <returns>The entry.</returns>
    public GitHubIssue Build() => new(
        _id,
        (int)_id,
        _pull,
        string.Create(CultureInfo.InvariantCulture, $"Issue {_id}"),
        _body,
        _state,
        "octocat",
        string.Create(CultureInfo.InvariantCulture, $"https://github.com/acme/widgets/{(_pull ? "pull" : "issues")}/{_id}"),
        _updatedAt.AddDays(-2),
        _updatedAt,
        _state == "closed" ? _updatedAt : null,
        ["bug"],
        _comments);

    /// <summary>Builds the entry.</summary>
    /// <param name="builder">The builder.</param>
    public static implicit operator GitHubIssue(GitHubIssueBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.Build();
    }
}
