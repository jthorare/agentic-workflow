using System.Collections.Concurrent;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Infrastructure.Launch;

namespace Aw.Sindri.Infrastructure.Sandbox.Launch;

/// <summary>
/// The sandbox step's launch-specific parts (<see cref="ISandboxLaunchStages"/>): the plan request from the approved profile, the login and the learn history, the egress, and what
/// the end of a launch releases. The session home itself is made and removed by <see cref="SandboxLaunch"/>.
/// </summary>
internal sealed class SandboxLaunchStages(IApprovedProfile profile, ILearnHistory learn, IReadOnlyList<IModelCredentialSource> logins, ISandboxLaunchInputs inputs, LaunchSandboxRegistry egress, TimeProvider clock, IAgentTokenSource agentTokens) : ISandboxLaunchStages
{
    private readonly ConcurrentDictionary<LaunchId, (SandboxPlan Plan, AgentId Target)> _plans = new();
    private readonly ConcurrentDictionary<LaunchId, SessionId?> _sessions = new();

    /// <inheritdoc/>
    public async ValueTask<Result<SandboxRequest>> Request(LaunchRequest request, SessionId? chosenSession, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        _sessions[request.Launch] = chosenSession ?? request.Resume;
        var approved = await profile.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return approved.Error;
        }

        var loaded = approved.Value?.Profile;
        var settings = loaded?.Sandbox;
        var repo = RepoNameOf(loaded, request.RepoPath);
        var prepared = await Login(request, settings, repo, cancellationToken).ConfigureAwait(false);
        if (prepared.IsError)
        {
            return prepared.Error;
        }

        var minted = await MintAgentToken(request, settings, cancellationToken).ConfigureAwait(false);
        if (minted.IsError)
        {
            return minted.Error;
        }

        var history = await learn.Read(repo.Value, cancellationToken).ConfigureAwait(false);
        var common = await inputs.GitCommonDir(request.RepoPath, cancellationToken).ConfigureAwait(false);
        var run = RunDirectory(request);
        return new SandboxRequest(
            request.Launch, request.Item, request.Agent, repo, request.Instance,
            new SandboxPaths(request.RepoPath, common, request.Worktree, run, inputs.RunAreaFor(request.Launch)),
            request.Instance is { } instance && request.Run is { } token ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"sindri/{WorkflowInstanceId.Of(instance).Value}/{token.Number}") : string.Empty,
            request.Step?.Policy, request.Attended,
            StepTimeout: null, LongMax: LongMax(loaded), SlotHeld: false,   // not on the launch request: the Step's own timeout and the heavy-slot fact
            new SandboxNetFacts(inputs.NetDirFor(request.Launch), inputs.SindriPort(), inputs.ProxyPort()),
            inputs.Host(), settings, prepared.Value)
        {
            Learn = new LearnContext(history, clock.GetUtcNow()),
        };
    }

    /// <inheritdoc/>
    public ValueTask<Result<PolicyFiles>> WritePolicy(SandboxRequest request, SandboxPlan plan, string sessionHome, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(plan);
        _plans[plan.Launch] = (plan, request.Target);
        if (request.Policy is not { } policy)
        {
            return Done(PolicyFiles.None);
        }

        var form = inputs.PolicyFormOf(request.Target);
        if (form == PolicyForm.ClaudeSettingsFile)
        {
            var from = StepPolicyFile.PathFor(inputs.PolicyDirectory(), plan.Launch);
            var to = StepPolicyFile.Write(sessionHome, plan.Launch, policy, SandboxPolicyOverlay.From(plan));
            StepPolicyFile.Delete(inputs.PolicyDirectory(), plan.Launch);   // the launcher's copy has no plan in it and the sandbox can't read it
            return Done(new PolicyFiles([], from, to));
        }

        return Done(form == PolicyForm.CodexOverrides ? new PolicyFiles(SandboxPolicyFiles.CodexOverrides(policy)) : PolicyFiles.None);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<Unit>> StartEgress(SandboxPlan plan, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var started = await egress.Start(plan.Launch, plan.Network, inputs.DenialCounterFor(plan.Launch), clock).ConfigureAwait(false);
        return started.IsError ? started.Error : Unit.Value;
    }

    /// <inheritdoc/>
    public async ValueTask Release(LaunchId launch, CancellationToken cancellationToken)
    {
        var known = _plans.TryRemove(launch, out var started);
        _ = _sessions.TryRemove(launch, out var chosen);
        if (known && egress.Get(launch)?.Denials is { } counter)
        {
            var session = DenialFileLocator.SessionFor(chosen, () => inputs.ReportedSession(launch));
            var (transcript, kind, failOpen) = DenialFileLocator.For(started.Plan, inputs.TranscriptGlob(started.Target), session);
            await DenialCollector.Collect(new DenialSources(started.Plan, SessionHomePaths.For(inputs.Host().SindriHome, launch), transcript, kind, failOpen), counter, cancellationToken).ConfigureAwait(false);   // before the egress stops and before session.ended
        }

        _ = await egress.Release(launch).ConfigureAwait(false);
    }

    private static ValueTask<Result<PolicyFiles>> Done(PolicyFiles files) => ValueTask.FromResult(Results.Success(files));

    private static TimeSpan? LongMax(SindriProfile? loaded) =>
        loaded is not null && RecipeDuration.TryFrom(LineProfile.LongMax(loaded), out var duration) ? duration.Span : null;

    private static RepoName RepoNameOf(SindriProfile? loaded, string repoPath) =>
        RepoName.From(loaded?.Repos?.FirstOrDefault(r => string.Equals(r.Path.TrimEnd('/'), repoPath.TrimEnd('/'), StringComparison.Ordinal))?.Name ?? Path.GetFileName(repoPath.TrimEnd('/')));

    // why: the same resolution PlanContext makes (an attended session never gets one); minting here makes a failure a Step failure, not an unconfined launch with no token
    private async ValueTask<Result<Unit>> MintAgentToken(LaunchRequest request, SandboxSettings? settings, CancellationToken cancellationToken)
    {
        var github = request.Attended ? GithubCredentials.None : settings?.Credentials?.Github ?? SandboxDefaults.Github;
        return github == GithubCredentials.AgentApp ? await agentTokens.Mint(request.Launch, cancellationToken).ConfigureAwait(false) : Unit.Value;
    }

    private async ValueTask<Result<PreparedLogin>> Login(LaunchRequest request, SandboxSettings? settings, RepoName repo, CancellationToken cancellationToken)
    {
        var source = LoginSourceResolver.Resolve(settings, request.Agent, repo, request.Step?.Policy?.LoginSource);
        var chosen = logins.FirstOrDefault(l => l.Id == source && l.Supports(request.Agent));
        return chosen is null
            ? SandboxRefusal.For(SindriErrors.LaunchLoginUnavailable, SandboxReasons.NoLogin, $"no {source} login source supports {request.Agent.Value}", source)
            : await chosen.Prepare(request.Agent, request.Launch, cancellationToken).ConfigureAwait(false);
    }

    // The launcher's run directory for the Step; a launch with none plans against its worktree, else the repo.
    private string RunDirectory(LaunchRequest request) => inputs.RunDirectoryFor(request) ?? request.Worktree ?? request.RepoPath;
}
