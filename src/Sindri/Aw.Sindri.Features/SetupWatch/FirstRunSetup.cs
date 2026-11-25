using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Templates;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.SetupWatch;

/// <summary>
/// The first-run steps the setup service does on its own once the profile names a repo (first-run spec decisions 10 and 19, E1 and E2): it sets 'workflow.agent.target' from the first agent the launcher finds,
/// then makes, activates and runs hello-sindri once per box (the 'setup.first_workflow.created' event). Neither is a question for the owner.
/// </summary>
/// <param name="live">The live profile.</param>
/// <param name="search">Finds the first installed agent (the launcher's catalog), not asking again on every tick while none is found.</param>
/// <param name="keys">Edits one key of the profile file in place.</param>
/// <param name="create">Makes the workflow (the command, run as 'service:setup').</param>
/// <param name="activate">Turns the workflow on (the command, run as 'service:setup').</param>
/// <param name="made">The ledger event that says hello-sindri was made once.</param>
/// <param name="run">The one run of hello-sindri, tried again on later ticks until it starts or can never start.</param>
internal sealed class FirstRunSetup(IProfileSource live, AgentTargetSearch search, IProfileKeyWriter keys, ISetupWorkflowCreator create, ISetupWorkflowActivator activate, FirstWorkflowEvents made, HelloRun run)
{
    private const string TargetKey = "workflow.agent.target";
    private static readonly WorkflowSlug _hello = WorkflowSlug.From("hello-sindri");

    /// <summary>Does the steps that are due; each one is skipped when it is done already, and a step that can't run now is tried again on the next tick.</summary>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>A task that ends when the due steps were tried.</returns>
    internal async ValueTask Run(CancellationToken cancellationToken)
    {
        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);
        if (loaded.IsError || loaded.Value.Profile.Repos is not { Count: > 0 } repos)
        {
            return;
        }

        await SetAgentTarget(loaded.Value.Profile, cancellationToken).ConfigureAwait(false);
        if (loaded.Value.Profile.Setup?.SkipFirstWorkflow != true && RepoName.TryFrom(repos[0].Name, out var first))
        {
            await MakeHello(first, cancellationToken).ConfigureAwait(false);
        }
    }

    // A line, not a question: the first agent whose binary the login shell finds becomes the target, unless the profile names one.
    private async ValueTask SetAgentTarget(SindriProfile profile, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(profile.Workflow?.Agent?.Target?.Value))
        {
            return;
        }

        if (await search.First(cancellationToken).ConfigureAwait(false) is not { } agent)
        {
            return;
        }

        var plan = keys.PlanText(TargetKey, agent);
        if (plan.IsSuccess)
        {
            _ = keys.Save(plan.Value);   // a failed write is tried again on the next tick
        }
    }

    // Create, activate, then record, then run: a crash between two of them is picked up on the next tick (a taken slug is the create that already happened, when setup made it).
    private async ValueTask MakeHello(RepoName repo, CancellationToken cancellationToken)
    {
        if (run.IsSettled() || await made.WasMade(cancellationToken).ConfigureAwait(false) is not { } wasMade)
        {
            return;   // settled, or the ledger can't say
        }

        if (wasMade)
        {
            await run.Once(_hello, cancellationToken).ConfigureAwait(false);   // the event is there; the run is retried until it starts or can never start
            return;
        }

        if (await Create(repo, cancellationToken).ConfigureAwait(false)
            && (await made.Made(_hello, repo, cancellationToken).ConfigureAwait(false)).IsSuccess)
        {
            await run.Once(_hello, cancellationToken).ConfigureAwait(false);
        }
    }

    // True when hello-sindri exists, is ours and is active. The owner's own hello-sindri is recorded (so setup stops trying) and left alone.
    private async ValueTask<bool> Create(RepoName repo, CancellationToken cancellationToken)
    {
        var template = GraphTemplates.Find("hello-sindri");
        var item = template?.Spec.Nodes.FirstOrDefault(static n => string.Equals(n.Type, "create_item", StringComparison.Ordinal));
        if (template is null || item is null)
        {
            return false;
        }

        item.Config["repo"] = repo.Value;
        var created = await create.Create(_hello, GraphJson.Write(template.Spec), cancellationToken).ConfigureAwait(false);
        if (created.IsError && !string.Equals(created.Error.Code, SindriErrors.GraphSlugTaken.Code, StringComparison.Ordinal))
        {
            return false;
        }

        if (created.IsError && !await TakenIsOurs(repo, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        var activated = await activate.Activate(_hello, cancellationToken).ConfigureAwait(false);
        return activated.IsSuccess || string.Equals(activated.Error.Code, SindriErrors.GraphArchived.Code, StringComparison.Ordinal);
    }

    // The slug was taken: true when setup made that workflow (a create that already happened). The owner's own hello-sindri is recorded (so setup stops trying) and left alone;
    // when the store can't say who made it, nothing is recorded and the next tick looks again.
    private async ValueTask<bool> TakenIsOurs(RepoName repo, CancellationToken cancellationToken)
    {
        var owner = await run.OwnerOf(_hello, cancellationToken).ConfigureAwait(false);
        if (owner == HelloOwner.NotOurs)
        {
            _ = await made.Made(_hello, repo, cancellationToken).ConfigureAwait(false);
            run.Settle();
        }

        return owner == HelloOwner.Ours;
    }
}
