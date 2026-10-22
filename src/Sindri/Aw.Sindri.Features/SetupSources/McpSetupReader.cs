using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Mcp;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.SetupSources;

/// <summary>
/// What the setup list reads of the per-repo MCP registration: for each repo the approved profile names, whether each installed provider's local registration exists for its checkout (Claude Code's local-scope entry, Codex's and Cursor's project files).
/// Each line also carries the installed providers' own states: connected (the entry exists), connecting (a RegisterMcp job for the repo is queued or running), failed (the newest such job failed) or not started.
/// Only local scope counts; a user-wide entry is never looked for here (doctor warns on that) and a file that can't be read is no entry. It starts nothing.
/// </summary>
/// <param name="approvals">The approved profile (the repos and their checkouts).</param>
/// <param name="host">Reads the providers' config files and says which are installed.</param>
/// <param name="jobs">The 'jobs' view (the RegisterMcp jobs).</param>
/// <param name="queue">The setup service's job queue (a job waiting for its turn is connecting too).</param>
internal sealed class McpSetupReader(IApprovedProfile approvals, IProviderHost host, IJobViews jobs, ISetupJobSlots queue)
{
    private static readonly JobKind _register = JobKind.From("RegisterMcp");
    private static readonly (ProviderFile Provider, string Name, string Id)[] _providers = [(ProviderFile.Claude, "Claude Code", "claude"), (ProviderFile.Codex, "Codex", "codex"), (ProviderFile.Cursor, "Cursor", "cursor")];

    internal async ValueTask<Result<McpFact>> Read(CancellationToken cancellationToken)
    {
        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return approved.Error;
        }

        var installed = _providers.Where(p => host.Installed(p.Provider)).ToList();
        var names = string.Join(", ", installed.Select(static p => p.Name));
        var repos = approved.Value?.Profile.Repos ?? [];
        if (repos.Count == 0 || installed.Count == 0)
        {
            return new McpFact(repos.Count, 0, names);
        }

        var claude = installed.Exists(static p => p.Provider == ProviderFile.Claude) ? await Text(host.UserConfigPath(ProviderFile.Claude), cancellationToken).ConfigureAwait(false) : null;
        var recent = await jobs.List(state: null, _register, cancellationToken).ConfigureAwait(false);
        var lines = new List<SetupItem>();
        foreach (var repo in repos)
        {
            lines.Add(await Line(installed, claude, repo, recent.IsSuccess ? recent.Value : [], cancellationToken).ConfigureAwait(false));
        }

        return new McpFact(repos.Count, lines.Count(static l => l.State == SetupState.Done), names) { Items = new SetupItems(lines) };
    }

    private async ValueTask<SetupItem> Line(List<(ProviderFile Provider, string Name, string Id)> installed, string? claude, RepoEntry repo, IReadOnlyList<JobRow> recent, CancellationToken cancellationToken)
    {
        var registeredIn = new List<string>();
        var states = new List<McpProviderState>();
        var (otherState, otherDetail) = Elsewhere(repo, recent);
        foreach (var (provider, name, id) in installed)
        {
            if (await IsRegistered(provider, claude, repo, cancellationToken).ConfigureAwait(false))
            {
                registeredIn.Add(name);
                states.Add(new McpProviderState(id, McpConnection.Connected));
            }
            else
            {
                states.Add(new McpProviderState(id, otherState, otherDetail));
            }
        }

        return new SetupItem(repo.Name, registeredIn.Count == installed.Count ? SetupState.Done : SetupState.Todo, registeredIn.Count == 0 ? null : string.Join(", ", registeredIn)) { Providers = states };
    }

    // What a provider without the entry stands at: a job queued or running for the repo is connecting; else the newest ended job says whether the last try failed.
    private (McpConnection State, string? Detail) Elsewhere(RepoEntry repo, IReadOnlyList<JobRow> recent)
    {
        var key = McpJobKeys.For(RepoName.From(repo.Name));
        var mine = recent.Where(j => j.Key == key).ToList();
        var queued = queue.Holds(_register, key);
        if (queued || mine.Exists(static j => j.State == JobState.Running))
        {
            return (McpConnection.Connecting, null);
        }

        return mine.FirstOrDefault(static j => j.State != JobState.Cancelled) is { State: JobState.Failed } failed
            ? (McpConnection.Failed, failed.Summary)
            : (McpConnection.NotStarted, null);
    }

    private async ValueTask<bool> IsRegistered(ProviderFile provider, string? claude, RepoEntry repo, CancellationToken cancellationToken) => provider switch
    {
        ProviderFile.Claude => McpLocalRegistration.IsRegistered(claude, repo.Path, repo.Name),
        ProviderFile.Cursor => McpLocalRegistration.IsRegisteredCursor(await Text(Path.Combine(repo.Path, ".cursor", "mcp.json"), cancellationToken).ConfigureAwait(false), repo.Name),
        ProviderFile.Codex => McpLocalRegistration.IsRegisteredCodex(await Text(Path.Combine(repo.Path, ".codex", "config.toml"), cancellationToken).ConfigureAwait(false), repo.Name),
        _ => false,
    };

    private async ValueTask<string?> Text(string path, CancellationToken cancellationToken)
    {
        try
        {
            return await host.Read(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;   // as registering reads it: a file that can't be read is no entry
        }
    }
}
