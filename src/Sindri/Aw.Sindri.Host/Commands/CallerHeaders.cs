using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Host.Commands;

/// <summary>The caller headers the CLI sends on every API call (API spec §6, "Who sends what").</summary>
internal sealed class CallerHeaders
{
    private readonly AgentSession? _session;
    private readonly IHumanTokenStore? _tokens;
    private readonly Func<CancellationToken, ValueTask<RepoName?>> _repo;
    private HumanToken? _token;
    private (bool Found, RepoName? Repo) _located;
    private string? _reason = NoToken;

    private CallerHeaders(AgentSession? session, IHumanTokenStore? tokens, Func<CancellationToken, ValueTask<RepoName?>> repo)
    {
        _session = session;
        _tokens = tokens;
        _repo = repo;
    }

    /// <summary>No headers at all (what S1's client sent): the API reads it as a session.</summary>
    internal static CallerHeaders None { get; } = new(session: null, tokens: null, static _ => ValueTask.FromResult<RepoName?>(null));

    private const string LaunchedReason = "a session Sindri launched (AW_SINDRI_LAUNCH is set)";

    private const string NoToken = "the per-start human token isn't readable";

    /// <summary>Why this CLI calls as a session, or null for a human (or for <see cref="None"/>); as of the last <see cref="Resolve"/> (each call makes one).</summary>
    internal string? SessionReason => _session is null ? null : _reason;

    /// <summary>
    /// Reads the per-start token: a readable one makes this CLI a human caller (whatever the terminal or the agent around it),
    /// an unreadable one a session.
    /// </summary>
    /// <param name="cancellationToken">Cancels the token read.</param>
    /// <returns>Why the CLI calls as a session, or null for a human.</returns>
    internal async ValueTask<string?> Resolve(CancellationToken cancellationToken)
    {
        _token = await ReadToken(cancellationToken).ConfigureAwait(false);
        _reason = _session is null || _token is not null ? null : _session.Launch is null ? NoToken : LaunchedReason;
        return _reason;
    }

    private async ValueTask<HumanToken?> ReadToken(CancellationToken cancellationToken)
    {
        if (_tokens is null || _session?.Launch is not null)   // a session Sindri launched never calls as a human, whatever it can read
        {
            return null;
        }

        var read = await _tokens.Read(cancellationToken).ConfigureAwait(false);
        return read.IsSuccess ? read.Value : null;
    }

    /// <summary>The headers for <paramref name="session"/>.</summary>
    /// <param name="session">How the CLI classed itself.</param>
    /// <param name="tokens">Where the per-start token is read from, at each call; readable: the call is a human's and carries it; none or unreadable: a session's, with no token.</param>
    /// <param name="repo">The X-Sindri-Repo value, found once at the first call.</param>
    /// <returns>The headers.</returns>
    internal static CallerHeaders For(AgentSession session, IHumanTokenStore? tokens, Func<CancellationToken, ValueTask<RepoName?>> repo) =>
        new(session, tokens, repo);

    /// <summary>Adds the headers to <paramref name="request"/>.</summary>
    /// <param name="request">The API call.</param>
    /// <param name="cancellationToken">Cancels the token read and the repo lookup.</param>
    /// <returns>The work.</returns>
    internal async ValueTask Apply(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_session is not { } session)
        {
            return;
        }

        var reason = await Resolve(cancellationToken).ConfigureAwait(false);
        var headers = request.Headers;
        headers.Add(SindriHeaders.Caller, reason is null ? "human" : "session");
        headers.Add(SindriHeaders.Client, reason is null && session.Prover ? "prover" : "cli");
        if (_token is { } token)
        {
            if (session.Terminal)
            {
                headers.Add(SindriHeaders.Terminal, "1");   // informational: it no longer decides authority
            }

            headers.Add(SindriHeaders.Token, token.Value);
        }

        if (session.Launch is { } launch)
        {
            headers.Add(SindriHeaders.Launch, launch);
        }

        if (await Repo(cancellationToken).ConfigureAwait(false) is { } repo)
        {
            headers.Add(SindriHeaders.Repo, repo.Value);
        }
    }

    /// <summary>The repo <paramref name="directory"/> belongs to among the approved profile's repos (API spec §3.1).</summary>
    /// <param name="approvals">The approved profile.</param>
    /// <param name="locator">Resolves the main checkout and matches it.</param>
    /// <param name="directory">The working directory.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The repo, or null (no approved profile, no repos, outside every repo).</returns>
    internal static async ValueTask<RepoName?> RepoOf(IApprovals approvals, IRepoLocator locator, string directory, CancellationToken cancellationToken)
    {
        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        return approved is { IsSuccess: true, Value.Profile.Repos: { Count: > 0 } repos }
            ? await locator.Locate(directory, repos, cancellationToken).ConfigureAwait(false)
            : null;
    }

    private async ValueTask<RepoName?> Repo(CancellationToken cancellationToken)
    {
        if (!_located.Found)
        {
            _located = (true, await _repo(cancellationToken).ConfigureAwait(false));
        }

        return _located.Repo;
    }
}
