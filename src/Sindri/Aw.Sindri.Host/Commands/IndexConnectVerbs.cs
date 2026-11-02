using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Secrets;
using Aw.Sindri.Host.IndexSources;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri index connect github|slack|linear': the human's verb (a launched session, or no terminal, is refused SND-AUTH-002), local, no control plane. It reads the token at the terminal, checks it against every listed repo and only then saves it
/// (owned-index spec §5.4, §5.8). Exit 0 connected, 2 refused or failed (the error names the fix).
/// </summary>
/// <param name="services">The CLI's scoped services.</param>
/// <param name="output">Where answers go.</param>
/// <param name="session">How the CLI classed itself: a launched session is never the human.</param>
internal sealed class IndexConnectVerbs(IServiceProvider services, Output output, AgentSession session)
{
    private const string Usage = "index connect github|slack|linear";

    internal async Task<int> Run(string[] args, CancellationToken ct)
    {
        return args switch
        {
            ["connect", "github"] => await Github(ct).ConfigureAwait(false),
            ["connect", "slack"] => await Slack(ct).ConfigureAwait(false),
            ["connect", "linear"] => await Linear(ct).ConfigureAwait(false),
            _ => await output.Error(SindriErrors.UnknownVerb.With($"'index {string.Join(' ', args)}' is not an index command: {Usage}")).ConfigureAwait(false),
        };
    }

    private async Task<int> Slack(CancellationToken ct)
    {
        var connect = new SlackConnect(
            services.GetRequiredService<ISecretPrompts>(),
            services.GetRequiredService<IProfileSource>(),
            services.GetRequiredService<ISecretWriter>(),
            services.GetRequiredService<ISecretPointers>(),
            services.GetRequiredService<ISourceTokenProfileWriter>(),
            SlackReaders.Create);
        var connected = await connect.Run(session.Human && session.Launch is null, ct).ConfigureAwait(false);
        if (connected.IsError)
        {
            return await output.Error(connected.Error).ConfigureAwait(false);
        }

        return await output.Text(
            $"slack connected: the token reads all {connected.Value.Channels.ToString(System.Globalization.CultureInfo.InvariantCulture)} listed channels and is stored at {connected.Value.Pointer}; the profile points to it.\n"
            + "Run `sindri profile approve` to start using it.\n", 0).ConfigureAwait(false);
    }

    private async Task<int> Linear(CancellationToken ct)
    {
        var connect = new LinearConnect(
            services.GetRequiredService<ISecretPrompts>(),
            services.GetRequiredService<IProfileSource>(),
            services.GetRequiredService<ISecretWriter>(),
            services.GetRequiredService<ISecretPointers>(),
            services.GetRequiredService<ISourceTokenProfileWriter>(),
            LinearTeamReader.Create);
        var connected = await connect.Run(session.Human && session.Launch is null, ct).ConfigureAwait(false);
        if (connected.IsError)
        {
            return await output.Error(connected.Error).ConfigureAwait(false);
        }

        return await output.Text(
            $"linear connected: the key reads all {connected.Value.Teams.ToString(System.Globalization.CultureInfo.InvariantCulture)} checked teams and is stored at {connected.Value.Pointer}; the profile points to it{Listed(connected.Value.Added)}.\n"
            + "Run `sindri profile approve` to start using it.\n", 0).ConfigureAwait(false);
    }

    private static string Listed(IReadOnlyList<string> added) =>
        added.Count == 0 ? string.Empty : $" and lists its {added.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)} teams: {string.Join(", ", added)}";

    private async Task<int> Github(CancellationToken ct)
    {
        var connect = new GitHubConnect(
            services.GetRequiredService<ISecretPrompts>(),
            services.GetRequiredService<IProfileSource>(),
            services.GetRequiredService<ISecretWriter>(),
            services.GetRequiredService<ISecretPointers>(),
            services.GetRequiredService<ISourceTokenProfileWriter>(),
            GitHubReaders.Create,
            services.GetRequiredService<TimeProvider>());
        var connected = await connect.Run(session.Human && session.Launch is null, ct).ConfigureAwait(false);
        if (connected.IsError)
        {
            return await output.Error(connected.Error).ConfigureAwait(false);
        }

        return await output.Text(
            $"github connected: the token reads all {connected.Value.Repos.ToString(System.Globalization.CultureInfo.InvariantCulture)} listed repos and is stored at {connected.Value.Pointer}; the profile points to it.\n"
            + "Run `sindri profile approve` to start using it.\n", 0).ConfigureAwait(false);
    }
}
