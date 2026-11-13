using Aw.Kernel;
using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Testing;

/// <summary>The GitHub account the Reviews tab reads with, set by the test.</summary>
public sealed class FakeGitHubAccount : IGitHubAccount
{
    /// <summary>Gets or sets where the connection stands.</summary>
    public GitHubAccountState State { get; set; } = GitHubAccountState.Missing;

    /// <summary>Gets or sets the connection's name ('github:work'), or null.</summary>
    public string? Connection { get; set; }

    /// <summary>Gets or sets the connection's GitHub login, or null.</summary>
    public string? Login { get; set; }

    /// <summary>Gets or sets why the login is null.</summary>
    public GitHubLoginMiss Miss { get; set; }

    /// <inheritdoc />
    public ValueTask<Result<GitHubAccount>> Current(CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success(new GitHubAccount(State, Connection, Login, Miss)));
}
