using Aw.Connectors.Core.GitHub;

namespace Aw.Connectors.Testing.GitHub;

/// <summary>Builds a <see cref="GitHubComment"/> with deterministic defaults (see <see cref="A.GitHubComment"/>).</summary>
public sealed class GitHubCommentBuilder
{
    private readonly long _id;
    private int _issue = 1;
    private string? _author = "hubot";
    private string _body = "Same here.";
    private DateTimeOffset _updatedAt = new(2026, 10, 1, 13, 0, 0, TimeSpan.Zero);

    internal GitHubCommentBuilder(long id)
    {
        _id = id;
    }

    /// <summary>Sets the issue or pull request number it is on.</summary>
    /// <param name="number">The number.</param>
    /// <returns>This builder.</returns>
    public GitHubCommentBuilder On(int number)
    {
        _issue = number;
        return this;
    }

    /// <summary>Sets the body.</summary>
    /// <param name="body">The markdown.</param>
    /// <returns>This builder.</returns>
    public GitHubCommentBuilder WithBody(string body)
    {
        _body = body;
        return this;
    }

    /// <summary>Sets the author.</summary>
    /// <param name="author">The login, or null.</param>
    /// <returns>This builder.</returns>
    public GitHubCommentBuilder By(string? author)
    {
        _author = author;
        return this;
    }

    /// <summary>Sets the change time; it was written an hour before.</summary>
    /// <param name="updatedAt">When it last changed.</param>
    /// <returns>This builder.</returns>
    public GitHubCommentBuilder UpdatedAt(DateTimeOffset updatedAt)
    {
        _updatedAt = updatedAt;
        return this;
    }

    /// <summary>Builds the comment.</summary>
    /// <returns>The comment.</returns>
    public GitHubComment Build() => new(_id, _issue, _author, _body, _updatedAt.AddHours(-1), _updatedAt);

    /// <summary>Builds the comment.</summary>
    /// <param name="builder">The builder.</param>
    public static implicit operator GitHubComment(GitHubCommentBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.Build();
    }
}
