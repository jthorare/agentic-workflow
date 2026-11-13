using System.Buffers;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.StartSession;

/// <summary>The checks before a start (agents spec §15.8): the repo's folder, the agent in the catalog, installed and signed in, and tmux present. Nothing is made yet.</summary>
/// <param name="approvals">The live profile, for the repo's folder.</param>
/// <param name="catalog">The agent catalog.</param>
/// <param name="checks">Whether the agent is ready and the folder exists.</param>
/// <param name="host">The tmux session host, for the tmux probe.</param>
internal sealed class StartPreflight(IApprovedProfile approvals, IAgentCatalog catalog, IStartChecks checks, ISessionHost host)
{
    internal async ValueTask<Result<StartPlan>> Check(StartSessionRequest request, CancellationToken cancellationToken)
    {
        var repo = await RepoPath(request, cancellationToken).ConfigureAwait(false);
        if (repo.IsError)
        {
            return repo.Error;
        }

        var agent = catalog.Resolve(request.Agent);
        if (agent is null)
        {
            return SindriErrors.AgentNotReady.With($"{request.Agent.Value} is not in the agent catalog");
        }

        var ready = await checks.Agent(agent, cancellationToken).ConfigureAwait(false);
        if (!ready.Installed || !ready.SignedIn)
        {
            return SindriErrors.AgentNotReady.With($"{agent.Label} is {(ready.Installed ? "not signed in" : "not installed")} (fix: {ready.SignInCommand})").WithExtension("command", Text(ready.SignInCommand));   // the form's Copy command button reads it
        }

        var facts = await host.Probe(cancellationToken).ConfigureAwait(false);
        return facts.Present
            ? new StartPlan(repo.Value, agent)
            : SindriErrors.AgentStartFailed.With($"tmux is not installed (fix: {InstallCommand()})");
    }

    private async ValueTask<Result<string>> RepoPath(StartSessionRequest request, CancellationToken cancellationToken)
    {
        var profile = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        var path = profile.IsSuccess ? profile.Value?.Profile.Repos?.FirstOrDefault(r => string.Equals(r.Name, request.Repo.Value, StringComparison.Ordinal))?.Path : null;
        if (path is null)
        {
            return SindriErrors.AgentRepoUnusable.With($"{request.Repo.Value} is not a repo of the profile");
        }

        return checks.FolderExists(path)
            ? path
            : SindriErrors.AgentRepoUnusable.With($"{request.Repo.Value}: the folder {path} is gone");
    }

    private static JsonElement Text(string text)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStringValue(text);
        }

        using var parsed = JsonDocument.Parse(buffer.WrittenMemory);
        return parsed.RootElement.Clone();
    }

    private static string InstallCommand() => OperatingSystem.IsMacOS() ? "brew install tmux" : "sudo apt install tmux";
}
