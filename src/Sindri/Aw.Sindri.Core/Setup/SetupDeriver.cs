using Aw.Kernel;

namespace Aw.Sindri.Core.Setup;

/// <summary>
/// Derives the eight steps in their fixed order (serve, sources, repo, profile, agent, mcp, map, first-workflow; first-run spec §3.1), each decided by <see cref="SetupSteps"/> from the fact its own source gave (setup-once spec §6: the inputs are read live).
/// A source that can't be read, or that throws, makes only its own step "failed" with a plain sentence, so the answer is never an error. Shared by GET /setup, the 'setup' service and doctor's 'setup' check.
/// </summary>
/// <param name="facts">The sources.</param>
/// <param name="sourceFailed">Told which step's source threw and with what, so the host can log it; null to stay quiet.</param>
public sealed class SetupDeriver(ISetupFacts facts, Action<string, Exception>? sourceFailed = null)
{
    /// <summary>Reads every source and decides every step.</summary>
    /// <param name="cancellationToken">Cancels the reads; only the caller's own cancellation passes through.</param>
    /// <returns>The eight steps.</returns>
    public async ValueTask<IReadOnlyList<SetupStep>> Derive(CancellationToken cancellationToken)
    {
        var serve = await Read("serve", facts.Serve, cancellationToken).ConfigureAwait(false);
        var sources = await Read("sources", facts.Sources, cancellationToken).ConfigureAwait(false);
        var repo = await Read("repo", facts.Repos, cancellationToken).ConfigureAwait(false);
        var profile = await Read("profile", facts.Profile, cancellationToken).ConfigureAwait(false);
        var agent = await Read("agent", facts.Agent, cancellationToken).ConfigureAwait(false);
        var mcp = await Read("mcp", facts.Mcp, cancellationToken).ConfigureAwait(false);
        var map = await Read("map", facts.Map, cancellationToken).ConfigureAwait(false);
        var first = await Read("first-workflow", facts.FirstWorkflow, cancellationToken).ConfigureAwait(false);
        SetupStep[] steps =
        [
            Step(SetupStepId.Serve, serve, SetupSteps.Serve),
            Step(SetupStepId.Sources, sources, SetupSteps.Sources),
            Step(SetupStepId.Repo, repo, SetupSteps.Repo),
            repo.IsError ? SetupSteps.Unreadable(SetupStepId.Profile, repo.Error.Code) : Step(SetupStepId.Profile, profile, p => SetupSteps.Profile(p, repo.Value)),
            repo.IsError ? SetupSteps.Unreadable(SetupStepId.Agent, repo.Error.Code) : Step(SetupStepId.Agent, agent, a => SetupSteps.Agent(a, repo.Value)),
            Step(SetupStepId.Mcp, mcp, SetupSteps.Mcp),
            Step(SetupStepId.Map, map, SetupSteps.Map),
            repo.IsError ? SetupSteps.Unreadable(SetupStepId.FirstWorkflow, repo.Error.Code) : Step(SetupStepId.FirstWorkflow, first, f => SetupSteps.FirstWorkflow(f, repo.Value)),
        ];
        return [.. steps.Select(static s => s with { Cli = SetupActions.CliFor(s.Action) })];   // the command an agent runs for the step's action; null for an owner-only one
    }

    private static SetupStep Step<T>(SetupStepId id, Result<T> read, Func<T, SetupStep> decide) =>
        read.IsError ? SetupSteps.Unreadable(id, read.Error.Code) : decide(read.Value);

    // Boundary: a source that throws fails only its own step, like one that returns an error: the answer is never a 500.
    // Only the request's own cancellation passes; a source's inner timeout (a TaskCanceledException on a live token) fails its step.
    private async ValueTask<Result<T>> Read<T>(string step, Func<CancellationToken, ValueTask<Result<T>>> source, CancellationToken cancellationToken)
    {
        try
        {
            return await source(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            sourceFailed?.Invoke(step, e);
            return SindriErrors.ApiInternal.With("a setup source failed");
        }
    }
}
