using Aw.Connectors.Core;
using Aw.Connectors.Core.Slack;
using Aw.Context.Core.Engine;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Host.IndexSources;

/// <summary>
/// 'sindri index connect slack' (owned-index spec §5.4, §10): the human reads the bot token at the terminal with the echo off, and Sindri checks it against every channel the profile lists (one cheap read each, through a reader that
/// refuses any other channel) before it saves anything. A direct message or a malformed id in the profile is refused before the token is even asked for. Only then is the token stored where a secret pointer can read it back and the
/// pointer (never the token) written into the profile. The token is never printed, logged or put in an error.
/// </summary>
/// <param name="prompts">The terminal.</param>
/// <param name="profiles">The live profile, for the channels.</param>
/// <param name="writer">Stores the token (the OS keychain, else an owner-only file).</param>
/// <param name="pointers">Reads the stored token back, to prove the pointer resolves.</param>
/// <param name="profile">Writes the pointer into the profile.</param>
/// <param name="readers">Makes a Slack reader for a token and the channel ids it may read.</param>
internal sealed class SlackConnect(
    ISecretPrompts prompts,
    IProfileSource profiles,
    ISecretWriter writer,
    ISecretPointers pointers,
    ISourceTokenProfileWriter profile,
    Func<string, IReadOnlyCollection<string>, ISlackReader> readers)
{
    private const string Service = "sindri-index";
    private const string Account = "slack";

    /// <summary>The prompt for the token.</summary>
    internal const string TokenPrompt = "Slack bot token (read-only: channels:history and channels:read, plus groups:history for private channels; not shown, not stored in the profile): ";

    /// <summary>Connects Slack.</summary>
    /// <param name="human">Whether the caller is a person at their own terminal; a session is refused before anything is asked.</param>
    /// <param name="cancellationToken">Cancels the asking and the checks.</param>
    /// <returns>The pointer now in the profile and how many channels were checked; SND-AUTH-002 for a session; SND-INDEX-011 when the profile lists no Slack channel; SND-INDEX-013 for a direct message or a channel the reader refuses; SND-INDEX-009 when Slack refuses the token or the stored pointer does not resolve; SND-INDEX-010 naming the channels the token cannot read.</returns>
    internal async ValueTask<Result<SlackConnected>> Run(bool human, CancellationToken cancellationToken)
    {
        if (!human)
        {
            return SindriErrors.AuthRefused.With("`sindri index connect slack` is the human's: run it in your own terminal, not from an agent session");
        }

        if (!prompts.Interactive)
        {
            return SindriErrors.InvalidInput.With("`sindri index connect slack` reads the token at the terminal and there is none here (fix: run it in your own terminal)");
        }

        var source = await Listed(cancellationToken).ConfigureAwait(false);
        if (source.IsError)
        {
            return source.Error;
        }

        var token = (await prompts.AskSecret(TokenPrompt, cancellationToken).ConfigureAwait(false))?.Trim();
        if (string.IsNullOrEmpty(token))
        {
            return SindriErrors.InvalidInput.With("no token was given; nothing was saved (fix: run `sindri index connect slack` again and paste the token)");
        }

        var checkedChannels = await CheckChannels(source.Value, token, cancellationToken).ConfigureAwait(false);
        return checkedChannels.IsError ? checkedChannels.Error : Save(token, source.Value.Channels.Count);
    }

    private Result<SlackConnected> Save(string token, int channels)
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

        var written = profile.SetToken("slack", stored.Value.ToString());
        return written.IsError ? written.Error : new SlackConnected(stored.Value.ToString(), channels);
    }

    private async ValueTask<Result<SlackSource>> Listed(CancellationToken cancellationToken)
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

        if (settings.Value.Slack is not { Channels.Count: > 0 } slack)
        {
            return SindriErrors.IndexNotInProfile.With("the profile lists no Slack channel; no request was sent (fix: list them under context.sources.slack.channels, then run `sindri index connect slack`)");
        }

        var refused = slack.Channels.Where(static c => !SlackChannelIds.IsReadable(c.Channel)).Select(static c => c.Name).ToList();
        return refused.Count == 0
            ? slack
            : ConnectorsErrors.ChannelNotAllowed.With($"{string.Join(", ", refused)} is a direct message or not a channel id; no request was sent (fix: list only channels, by their C… or G… id, under context.sources.slack.channels)");
    }

    // Every listed channel, one cheap read each, before anything is saved. A refused token ends the check at once; unreadable channels are all named.
    private async ValueTask<Result<Unit>> CheckChannels(SlackSource slack, string token, CancellationToken cancellationToken)
    {
        var reader = readers(token, [.. slack.Channels.Select(static c => c.Channel)]);
        var unreadable = new List<string>();
        foreach (var channel in slack.Channels)
        {
            var probe = await reader.Probe(channel.Channel, cancellationToken).ConfigureAwait(false);
            if (probe.IsSuccess && probe.Value is null)
            {
                continue;
            }

            if (probe.IsSuccess)
            {
                return ConnectorsErrors.RateLimited.With("Slack's rate limit stopped the check; nothing was saved (fix: run `sindri index connect slack` again in a minute)");
            }

            if (probe.Error.Code == ConnectorsErrors.Unauthorized.Code)
            {
                return SindriErrors.IndexTokenUnresolved.With("Slack refused the token; nothing was saved (fix: make a bot token with channels:history and channels:read and run `sindri index connect slack` again)");
            }

            if (probe.Error.Code == ConnectorsErrors.NotFound.Code || probe.Error.Code == ConnectorsErrors.AccessDenied.Code)
            {
                unreadable.Add(channel.Name);
                continue;
            }

            return probe.Error;   // not allowed, unreachable, a refused host: nothing says the scope is wrong, and nothing was saved
        }

        return unreadable.Count == 0
            ? Unit.Value
            : SindriErrors.IndexRepoUnreadable.With($"the token cannot read {string.Join(", ", unreadable)}; nothing was saved (fix: invite the bot to them and grant its scopes, or remove them from context.sources.slack.channels)");
    }
}
