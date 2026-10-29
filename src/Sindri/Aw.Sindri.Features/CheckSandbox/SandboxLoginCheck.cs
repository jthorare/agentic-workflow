using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Credentials;

namespace Aw.Sindri.Features.CheckSandbox;

/// <summary>
/// The login probe (sandbox spec §3.4, §6): for the agent Steps run (the approved profile's 'workflow.agent.target', when installed, approved for Steps and its policy form is expressible), the credential source the profile picks (a repo's, else the
/// agent's, else the profile's) must deliver the model login. Never tries another source. Distinct failures are listed once. With no target (or an unreadable profile) Steps go to the desk, so there is nothing to check; any other installed agent is no blocker.
/// </summary>
/// <param name="setup">The catalog and what is installed.</param>
/// <param name="catalog">The entries' policy forms.</param>
/// <param name="approved">The approved profile.</param>
/// <param name="sources">The credential sources.</param>
internal sealed class SandboxLoginCheck(ILaunchSetup setup, IAgentCatalog catalog, IApprovedProfile approved, ModelCredentialSources sources) : ISandboxLoginCheck
{
    private static readonly TimeSpan _needed = SandboxLoginTarget.Needed;

    public async ValueTask<IReadOnlyList<SandboxBlocker>> Check(CancellationToken cancellationToken)
    {
        if (await SandboxLoginTarget.Find(setup, catalog, approved, cancellationToken).ConfigureAwait(false) is not { } target)
        {
            return [];
        }

        var (stepAgent, settings) = target;
        var found = new Dictionary<(string Target, string Source, string Reason), SandboxBlocker>();
        Collect(found, settings, stepAgent, RepoNames(settings));
        return [.. found.Values];
    }

    private void Collect(Dictionary<(string Target, string Source, string Reason), SandboxBlocker> found, SandboxSettings? settings, AgentId target, List<RepoName> repos)
    {
        foreach (var blocker in repos.Select(repo => Probe(settings, target, repo)).OfType<SandboxBlocker>())
        {
            _ = found.TryAdd((target.Value, blocker.Source ?? string.Empty, blocker.Reason), blocker);
        }
    }

    /// <summary>A name for 'no repo in particular', then every repo the profile's sandbox section names (a repo may pick its own source).</summary>
    private static List<RepoName> RepoNames(SandboxSettings? settings)
    {
        var repos = new List<RepoName> { RepoName.From("sandbox-check") };
        repos.AddRange((settings?.Repos?.Keys ?? []).Where(static k => RepoName.TryParse(k, provider: null, out _)).Select(RepoName.From));
        return repos;
    }

    private SandboxBlocker? Probe(SandboxSettings? settings, AgentId target, RepoName repo)
    {
        var source = LoginSourceResolver.Resolve(settings, target, repo, stepAsk: null);
        if (sources.Check(source, target, _needed) is not { IsError: true } failed)
        {
            return null;
        }

        var blocker = SandboxBlocker.Of(failed.Error);
        var values = new Dictionary<string, string>(blocker.Values ?? new Dictionary<string, string>(StringComparer.Ordinal), StringComparer.Ordinal) { ["target"] = target.Value };
        return blocker with { Values = values };
    }

}
