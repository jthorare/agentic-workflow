using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Secrets;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.SetupSources;

/// <summary>
/// What the sources step reads: the github, slack and linear sources the approved profile names (linear once it names a token, a url or a team), and whether each token pointer resolves, decided by
/// <see cref="SourceTokens"/> as doctor does. A read; no source is called and no token value is kept.
/// </summary>
/// <param name="approvals">The approved profile.</param>
/// <param name="engine">The context engine, which checks the profile's context section.</param>
/// <param name="secrets">Resolves a token pointer.</param>
internal sealed class SourcesSetupReader(IApprovedProfile approvals, IContextEngine engine, ISecretPointers secrets)
{
    internal async ValueTask<Result<SourcesFact>> Read(CancellationToken cancellationToken)
    {
        var latest = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (latest.IsError)
        {
            return latest.Error;
        }

        if (latest.Value is not { } loaded)
        {
            return new SourcesFact(0);
        }

        ProfileRepoEntry[] entries = [.. (loaded.Profile.Repos ?? []).Select(r => new ProfileRepoEntry(r.Name, r.Path))];
        var settings = engine.CheckSettings(loaded.Profile.Context, entries);
        if (settings.IsError)
        {
            return settings.Error;
        }

        List<SetupItem> lines = [];
        if (settings.Value.GitHub is { } github)
        {
            lines.Add(Line("github", github.Token));
        }

        if (settings.Value.Slack is { } slack)
        {
            lines.Add(Line("slack", slack.Token));
        }

        if (settings.Value.Linear is { } linear)
        {
            lines.Add(Line("linear", linear.Token));
        }
        else if (settings.Value.LinearTeams.Count > 0)
        {
            lines.Add(Line("linear", pointer: null));
        }

        return new SourcesFact(lines.Count) { Items = new SetupItems(lines) };
    }

    private SetupItem Line(string source, string? pointer) => SourceTokens.Connected(pointer, secrets)
        ? new SetupItem(source, SetupState.Done, "connected")
        : new SetupItem(source, SetupState.Todo, "needs token");
}
