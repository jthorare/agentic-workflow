using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Host.IndexSources;

/// <summary>
/// 'sindri index connect linear' (issue 1617): the human reads the Linear API key at the terminal with the echo off, and Sindri checks it against Linear's teams before it saves anything: every team in
/// context.sources.linear.teams must be readable, and with none listed the key must read at least one. Only then is the key stored where a secret pointer can read it back and the pointer (never the key)
/// written into the profile. The key is never printed, logged or put in an error.
/// </summary>
/// <param name="prompts">The terminal.</param>
/// <param name="profiles">The live profile, for the teams and the endpoint.</param>
/// <param name="writer">Stores the key (the OS keychain, else an owner-only file).</param>
/// <param name="pointers">Reads the stored key back, to prove the pointer resolves.</param>
/// <param name="profile">Writes the pointer into the profile.</param>
/// <param name="readers">Makes a team reader for the endpoint and the key.</param>
internal sealed class LinearConnect(
    ISecretPrompts prompts,
    IProfileSource profiles,
    ISecretWriter writer,
    ISecretPointers pointers,
    ISourceTokenProfileWriter profile,
    Func<Uri, string, ILinearTeamReader> readers)
{
    private const string Service = "sindri-index";
    private const string Account = "linear";

    /// <summary>The prompt for the key.</summary>
    internal const string KeyPrompt = "Linear API key (a read-only personal API key; not shown, not stored in the profile): ";

    /// <summary>Linear's own GraphQL endpoint, used when the profile names no url.</summary>
    internal static readonly Uri DefaultUrl = new("https://api.linear.app/graphql");

    /// <summary>Connects Linear.</summary>
    /// <param name="human">Whether the caller is a person at their own terminal; a session is refused before anything is asked.</param>
    /// <param name="cancellationToken">Cancels the asking and the checks.</param>
    /// <returns>The pointer now in the profile and how many teams were checked; SND-AUTH-002 for a session; SND-INDEX-009 when Linear refuses the key or the stored pointer does not resolve; SND-INDEX-010 naming the listed teams the key cannot read, or saying it reads none; SND-CONTEXT-010 when Linear is unreachable.</returns>
    internal async ValueTask<Result<LinearConnected>> Run(bool human, CancellationToken cancellationToken)
    {
        if (!human)
        {
            return SindriErrors.AuthRefused.With("`sindri index connect linear` is the human's: run it in your own terminal, not from an agent session");
        }

        if (!prompts.Interactive)
        {
            return SindriErrors.InvalidInput.With("`sindri index connect linear` reads the key at the terminal and there is none here (fix: run it in your own terminal)");
        }

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

        var key = await AskKey(cancellationToken).ConfigureAwait(false);
        if (key.IsError)
        {
            return key.Error;
        }

        var url = settings.Value.Linear?.Url ?? DefaultUrl;
        var checkedTeams = await CheckTeams(settings.Value.LinearTeams, url, key.Value, cancellationToken).ConfigureAwait(false);
        return checkedTeams.IsError ? checkedTeams.Error : Save(key.Value, url, checkedTeams.Value);
    }

    private async ValueTask<Result<string>> AskKey(CancellationToken cancellationToken)
    {
        var key = (await prompts.AskSecret(KeyPrompt, cancellationToken).ConfigureAwait(false))?.Trim();
        if (string.IsNullOrEmpty(key))
        {
            return SindriErrors.InvalidInput.With("no key was given; nothing was saved (fix: run `sindri index connect linear` again and paste the key)");
        }

        return key.Any(static c => char.IsControl(c) || char.IsWhiteSpace(c))
            ? SindriErrors.InvalidInput.With("the key has spaces or control characters, so it is not a Linear API key; nothing was saved (fix: run `sindri index connect linear` again and paste only the key)")
            : key;
    }

    // A loopback Linear (the fixture's fake) takes only a file: pointer (LinearSettings, SND-CONTEXT-070), so its key never goes to the keychain.
    private Result<LinearConnected> Save(string key, Uri url, CheckedTeams teams)
    {
        var stored = url == DefaultUrl ? writer.Store(Service, Account, key) : writer.StoreInFile(Service, Account, key);
        if (stored.IsError)
        {
            return stored.Error;
        }

        var resolved = pointers.Resolve(stored.Value);
        if (resolved.IsError)
        {
            return SindriErrors.IndexTokenUnresolved.With($"the key was stored at {stored.Value} but that pointer does not resolve: {resolved.Error.Message}");
        }

        var written = profile.SetToken("linear", stored.Value.ToString());
        if (written.IsError)
        {
            return written.Error;
        }

        // No teams listed: list the ones the key reads, or the Projects page (which shows only listed teams) would stay empty. The owner still approves the profile.
        if (teams.Added.Count > 0)
        {
            var listed = profile.SetTeams("linear", teams.Added);
            if (listed.IsError)
            {
                return listed.Error;
            }
        }

        return new LinearConnected(stored.Value.ToString(), teams.Count, teams.Added);
    }

    // One read of the teams the key can see, before anything is saved: a refused key ends it at once, a listed team missing from the answer is named.
    private async ValueTask<Result<CheckedTeams>> CheckTeams(IReadOnlyList<string> listed, Uri url, string key, CancellationToken cancellationToken)
    {
        var read = await readers(url, key).Teams(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error.Code == ContextErrors.LinearKeyRefused.Code
                ? SindriErrors.IndexTokenUnresolved.With("Linear refused the key; nothing was saved (fix: create a read-only personal API key in Linear and run `sindri index connect linear` again)")
                : read.Error;   // unreachable, rate limited, an unexpected reply: nothing says the key is wrong, and nothing was saved
        }

        if (listed.Count == 0)
        {
            return read.Value.Count > 0
                ? new CheckedTeams(read.Value.Count, [.. read.Value.Order(StringComparer.Ordinal)])
                : SindriErrors.IndexRepoUnreadable.With("the key reads no Linear team; nothing was saved (fix: use a key from an account that belongs to a team, then run `sindri index connect linear` again)");
        }

        var unreadable = listed.Where(t => !read.Value.Contains(t, StringComparer.Ordinal)).ToList();
        return unreadable.Count == 0
            ? new CheckedTeams(listed.Count, [])
            : SindriErrors.IndexRepoUnreadable.With($"the key cannot read {string.Join(", ", unreadable)}; nothing was saved (fix: use a key whose account belongs to them, or remove them from context.sources.linear.teams)");
    }

    private sealed record CheckedTeams(int Count, IReadOnlyList<string> Added);
}
