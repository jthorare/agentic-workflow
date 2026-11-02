using System.Globalization;
using Aw.Connectors.Core;
using Aw.Connectors.Core.GitHub;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Connectors.Features.PullGitHub;

/// <summary>
/// The GitHub connector over every repo the profile lists (owned-index spec §5.3): one <see cref="PullGitHubRequest"/> per repo, in the listed order, each repo from its own cursor (a repo's first pull reaches back by the window), until the limit is
/// reached. A rate limit or a failed repo ends the run with what was read so far and the cursor for it; when nothing was read the pull is an error row (AW-CONNECTORS-009 carries the time the limit lifts), never an exception.
/// </summary>
/// <param name="repos">The listed repos, 'owner/name'.</param>
/// <param name="commentCap">The most comments read per record.</param>
/// <param name="window">How far back a repo's first pull reaches.</param>
/// <param name="pull">Pulls one repo.</param>
/// <param name="reader">Probes the token and the reach.</param>
/// <param name="clock">The clock the window reads.</param>
internal sealed class GitHubConnector(
    IReadOnlyList<string> repos,
    int commentCap,
    TimeSpan window,
    IService<PullGitHubRequest, PullGitHubResponse> pull,
    IGitHubReader reader,
    TimeProvider clock) : IConnector
{
    /// <inheritdoc />
    public ConnectorDescription Describe() => new("github", "GitHub");

    /// <inheritdoc />
    public async ValueTask<Result<ConnectorPage>> Pull(SourceCursor? after, int limit, DateTimeOffset? before, CancellationToken cancellationToken)
    {
        var cursors = GitHubCursors.Parse(after?.Value);
        var items = new List<ConnectorItem>();
        Error? stopped = null;
        foreach (var repo in repos)
        {
            if (items.Count >= limit)
            {
                break;
            }

            var from = cursors.TryGetValue(repo, out var known) ? known : GitHubCursor.From(clock.GetUtcNow() - window);
            var one = await pull.Handle(new PullGitHubRequest(repo, from, limit - items.Count) { CommentCap = commentCap, Before = before }, cancellationToken).ConfigureAwait(false);
            if (one.IsError)
            {
                stopped = one.Error;
                break;
            }

            items.AddRange(one.Value.Items);
            cursors[repo] = one.Value.Next;
            if (one.Value.RateLimitedUntil is { } until)
            {
                stopped = ConnectorsErrors.RateLimited.With($"GitHub's rate limit stopped the run on {repo}; it lifts at {until.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)}");
                break;
            }
        }

        return stopped is { } error && items.Count == 0
            ? error
            : new ConnectorPage(items, [], new Aw.Connectors.Core.SourceCursor(GitHubCursors.Format(cursors)), CursorReset: false);
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyDictionary<string, string>>> Versions(IReadOnlyList<string> keys, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<IReadOnlyDictionary<string, string>>>(ConnectorsErrors.Invalid.With("the GitHub connector does not read versions yet"));

    /// <inheritdoc />
    public async ValueTask<Result<Unit>> Probe(CancellationToken cancellationToken)
    {
        if (repos.Count == 0)
        {
            return Unit.Value;
        }

        var read = await reader.Issues(repos[0], GitHubCursor.From(clock.GetUtcNow()), 1, before: null, cancellationToken).ConfigureAwait(false);
        return read.IsError ? read.Error : Unit.Value;
    }
}
