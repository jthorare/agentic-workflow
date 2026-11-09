using Aw.Sindri.Core;
using Aw.Sindri.Core.Linear;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Secrets;
using Aw.Sindri.Host.IndexSources;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri index connect github|slack|linear [--token-env NAME]': local, no control plane; the owner and an agent session run it alike. It reads the token at the terminal (or from the environment variable NAME, for a session with no terminal), checks it against every listed repo and only then saves it
/// (owned-index spec §5.4, §5.8). Exit 0 connected, 2 refused or failed (the error names the fix).
/// </summary>
/// <param name="services">The CLI's scoped services.</param>
/// <param name="output">Where answers go.</param>
/// <param name="environment">Reads an environment variable, for --token-env.</param>
internal sealed class IndexConnectVerbs(IServiceProvider services, Output output, Func<string, string?> environment)
{
    private const string Usage = "index connect github|slack|linear [--token-env NAME]";

    internal async Task<int> Run(string[] args, CancellationToken ct)
    {
        var prompts = Prompts(args, out var words, out var problem);
        if (problem is not null)
        {
            return await output.Error(SindriErrors.InvalidInput.With(problem)).ConfigureAwait(false);
        }

        return words switch
        {
            ["connect", "github"] => await Github(prompts, ct).ConfigureAwait(false),
            ["connect", "slack"] => await Slack(prompts, ct).ConfigureAwait(false),
            ["connect", "linear"] => await Linear(prompts, ct).ConfigureAwait(false),
            _ => await output.Error(SindriErrors.UnknownVerb.With($"'index {string.Join(' ', words)}' is not an index command: {Usage}")).ConfigureAwait(false),
        };
    }

    // --token-env NAME swaps the terminal prompt for the variable's value; the rest of the arguments are the verb's words.
    private ISecretPrompts Prompts(string[] args, out string[] words, out string? problem)
    {
        problem = null;
        var at = Array.IndexOf(args, "--token-env");
        if (at < 0)
        {
            words = args;
            return services.GetRequiredService<ISecretPrompts>();
        }

        words = [.. args.Take(at), .. args.Skip(at + 2)];
        if (at + 1 >= args.Length)
        {
            problem = "--token-env needs the name of an environment variable (" + Usage + ")";
            return services.GetRequiredService<ISecretPrompts>();
        }

        var name = args[at + 1];
        if (environment(name) is not { } value || string.IsNullOrWhiteSpace(value))
        {
            problem = $"--token-env names {name}, which is not set or is empty (spaces only counts as empty) (fix: export it with the token, then run this again)";
            return services.GetRequiredService<ISecretPrompts>();
        }

        return new EnvSecretPrompts(value);
    }

    private async Task<int> Slack(ISecretPrompts prompts, CancellationToken ct)
    {
        var connect = new SlackConnect(
            prompts,
            services.GetRequiredService<IProfileSource>(),
            services.GetRequiredService<ISecretWriter>(),
            services.GetRequiredService<ISecretPointers>(),
            services.GetRequiredService<ISourceTokenProfileWriter>(),
            SlackReaders.Create);
        var connected = await connect.Run(ct).ConfigureAwait(false);
        if (connected.IsError)
        {
            return await output.Error(connected.Error).ConfigureAwait(false);
        }

        return await output.Text(
            $"slack connected: the token reads all {connected.Value.Channels.ToString(System.Globalization.CultureInfo.InvariantCulture)} listed channels and is stored at {connected.Value.Pointer}; the profile points to it.\n"
            + "It is live now: edits apply on save.\n", 0).ConfigureAwait(false);
    }

    private async Task<int> Linear(ISecretPrompts prompts, CancellationToken ct)
    {
        var connect = new LinearConnect(
            prompts,
            services.GetRequiredService<IProfileSource>(),
            services.GetRequiredService<ISecretWriter>(),
            services.GetRequiredService<ISecretPointers>(),
            services.GetRequiredService<ISourceTokenProfileWriter>(),
            LinearTeamReader.Create,
            new LinearCheckRecorder(services.GetRequiredService<ILinearCheckStore>(), services.GetRequiredService<TimeProvider>()));
        var connected = await connect.Run(ct).ConfigureAwait(false);
        if (connected.IsError)
        {
            return await output.Error(connected.Error).ConfigureAwait(false);
        }

        return await output.Text(
            $"linear connected: the key reads all {connected.Value.Teams.ToString(System.Globalization.CultureInfo.InvariantCulture)} checked teams and is stored at {connected.Value.Pointer}; the profile points to it{Listed(connected.Value.Added)}.\n"
            + "It is live now: edits apply on save.\n", 0).ConfigureAwait(false);
    }

    private static string Listed(IReadOnlyList<string> added) =>
        added.Count == 0 ? string.Empty : $" and lists its {added.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)} teams: {string.Join(", ", added)}";

    private async Task<int> Github(ISecretPrompts prompts, CancellationToken ct)
    {
        var connect = new GitHubConnect(
            prompts,
            services.GetRequiredService<IProfileSource>(),
            services.GetRequiredService<ISecretWriter>(),
            services.GetRequiredService<ISecretPointers>(),
            services.GetRequiredService<ISourceTokenProfileWriter>(),
            GitHubReaders.Create,
            services.GetRequiredService<TimeProvider>());
        var connected = await connect.Run(ct).ConfigureAwait(false);
        if (connected.IsError)
        {
            return await output.Error(connected.Error).ConfigureAwait(false);
        }

        return await output.Text(
            $"github connected: the token reads all {connected.Value.Repos.ToString(System.Globalization.CultureInfo.InvariantCulture)} listed repos and is stored at {connected.Value.Pointer}; the profile points to it.\n"
            + "It is live now: edits apply on save.\n", 0).ConfigureAwait(false);
    }
}
