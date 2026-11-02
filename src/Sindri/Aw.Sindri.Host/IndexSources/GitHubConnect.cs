using Aw.Connectors.Core;
using Aw.Connectors.Core.GitHub;
using Aw.Context.Core.Engine;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Host.IndexSources;

/// <summary>
/// 'sindri index connect github' (owned-index spec §5.4, §5.8): the human reads the token at the terminal with the echo off, and Sindri checks the token against every repo the profile lists before it saves anything, so a wrong scope
/// fails here and not hours into a sync. Only then is the token stored where a secret pointer can read it back, and the pointer (never the token) written into the profile. The token is never printed, logged or put in an error.
/// </summary>
/// <param name="prompts">The terminal.</param>
/// <param name="profiles">The live profile, for the repos and the host.</param>
/// <param name="writer">Stores the token (the OS keychain, else an owner-only file).</param>
/// <param name="pointers">Reads the stored token back, to prove the pointer resolves.</param>
/// <param name="profile">Writes the pointer into the profile.</param>
/// <param name="readers">Makes a GitHub reader for a host, the private-host switch and a token.</param>
/// <param name="clock">The clock the checks read.</param>
internal sealed class GitHubConnect(
    ISecretPrompts prompts,
    IProfileSource profiles,
    ISecretWriter writer,
    ISecretPointers pointers,
    ISourceTokenProfileWriter profile,
    Func<string?, bool, string, IGitHubReader> readers,
    TimeProvider clock)
{
    private const string Service = "sindri-index";
    private const string Account = "github";

    /// <summary>The prompt for the token.</summary>
    internal const string TokenPrompt = "GitHub token (read-only: Issues, Pull requests and Metadata read on the listed repos; not shown, not stored in the profile): ";

    /// <summary>Connects GitHub.</summary>
    /// <param name="human">Whether the caller is a person at their own terminal; a session is refused before anything is asked.</param>
    /// <param name="cancellationToken">Cancels the asking and the checks.</param>
    /// <returns>The pointer now in the profile and how many repos were checked; SND-AUTH-002 for a session; SND-INDEX-011 when the profile lists no GitHub repo; SND-INDEX-009 when GitHub refuses the token or the stored pointer does not resolve; SND-INDEX-010 naming the repos the token cannot read.</returns>
    internal async ValueTask<Result<GitHubConnected>> Run(bool human, CancellationToken cancellationToken)
    {
        if (!human)
        {
            return SindriErrors.AuthRefused.With("`sindri index connect github` is the human's: run it in your own terminal, not from an agent session");
        }

        if (!prompts.Interactive)
        {
            return SindriErrors.InvalidInput.With("`sindri index connect github` reads the token at the terminal and there is none here (fix: run it in your own terminal)");
        }

        var source = await Listed(cancellationToken).ConfigureAwait(false);
        if (source.IsError)
        {
            return source.Error;
        }

        var token = (await prompts.AskSecret(TokenPrompt, cancellationToken).ConfigureAwait(false))?.Trim();
        if (string.IsNullOrEmpty(token))
        {
            return SindriErrors.InvalidInput.With("no token was given; nothing was saved (fix: run `sindri index connect github` again and paste the token)");
        }

        var checkedRepos = await CheckRepos(source.Value, token, cancellationToken).ConfigureAwait(false);
        if (checkedRepos.IsError)
        {
            return checkedRepos.Error;
        }

        return Save(token, source.Value.Repos.Count);
    }

    // Only after every repo checked: store the token, prove its pointer resolves, then point the profile at it.
    private Result<GitHubConnected> Save(string token, int repos)
    {
        var stored = writer.Store(Service, Account, token);
        if (stored.IsError)
        {
            return stored.Error;
        }

        var resolved = pointers.Resolve(stored.Value);
        if (resolved.IsError)
        {
            return SindriErrors.IndexTokenUnresolved.With($"the token was stored at {stored.Value} but that pointer does not resolve: {resolved.Error.Message}");
        }

        var written = profile.SetToken("github", stored.Value.ToString());
        return written.IsError ? written.Error : new GitHubConnected(stored.Value.ToString(), repos);
    }

    private async ValueTask<Result<GitHubSource>> Listed(CancellationToken cancellationToken)
    {
        var loaded = await profiles.Load(cancellationToken).ConfigureAwait(false);
        if (loaded.IsError)
        {
            return loaded.Error;
        }

        var settings = SettingsReader.Read(loaded.Value.Profile.Context);
        if (settings.IsError)
        {
            return settings.Error;
        }

        return settings.Value.GitHub is { Repos.Count: > 0 } github
            ? github
            : SindriErrors.IndexNotInProfile.With("the profile lists no GitHub repo; no request was sent (fix: list them under context.sources.github.repos, then run `sindri index connect github`)");
    }

    // Every listed repo, one cheap read each, before anything is saved. A refused token ends the check at once; unreadable repos are all named.
    private async ValueTask<Result<Unit>> CheckRepos(GitHubSource github, string token, CancellationToken cancellationToken)
    {
        var reader = readers(github.Host, github.AllowPrivateHost, token);
        var unreadable = new List<string>();
        foreach (var repo in github.Repos)
        {
            var read = await reader.Issues(repo, GitHubCursor.From(clock.GetUtcNow()), 1, before: null, cancellationToken).ConfigureAwait(false);
            if (read.IsSuccess)
            {
                continue;
            }

            if (read.Error.Code == ConnectorsErrors.Unauthorized.Code)
            {
                return SindriErrors.IndexTokenUnresolved.With("GitHub refused the token; nothing was saved (fix: make a fine-grained read-only token and run `sindri index connect github` again)");
            }

            if (read.Error.Code is var code && (code == ConnectorsErrors.NotFound.Code || code == ConnectorsErrors.AccessDenied.Code))
            {
                unreadable.Add(repo);
                continue;
            }

            return read.Error;   // unreachable, a refused host, a rate limit: nothing says the scope is wrong, and nothing was saved
        }

        return unreadable.Count == 0
            ? Unit.Value
            : SindriErrors.IndexRepoUnreadable.With($"the token cannot read {string.Join(", ", unreadable)}; nothing was saved (fix: widen the token to them, or remove them from context.sources.github.repos)");
    }
}
