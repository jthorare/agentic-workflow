using Aw.Context.Core.Extract;
using Aw.Context.Core.Models;
using Aw.Context.Core.Trust;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Labeling;

/// <summary>
/// Names clusters with a model through <see cref="IModelPort"/>, within a token cap (SND-CONTEXT-042 when the pass would go over it). It sends file paths only, scrubbed, in packs sized for the smallest prompt budget of the labeling route
/// (<see cref="LabelGenerator"/>); what comes back is untrusted text, so a label is cleaned (control characters, runs of spaces, 60 characters) and only an id that was asked about is kept. A model error after some packs answered keeps their labels and
/// marks the pass incomplete; before any answer it is the error.
/// </summary>
/// <param name="model">The model port.</param>
/// <param name="scrubbers">Builds the scrubbers the prompt passes through.</param>
/// <param name="choice">The Jev pick (spec §6), or null when the host has none: the pass then asks for one label per cluster.</param>
/// <param name="budget">The smallest prompt budget of the labeling route (spec §6.2), or null for the wide one.</param>
internal sealed class ModelLabeler(IModelPort model, IScrubberFactory scrubbers, IChoicePort? choice = null, IModelPromptBudget? budget = null) : ILabeler
{
    public async ValueTask<Result<LabelPass>> Label(IReadOnlyList<FileCluster> clusters, IReadOnlyList<ScrubPattern> extraPatterns, int maxTokens, LabelOptions? options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(clusters);
        var built = scrubbers.For(extraPatterns);
        if (built.IsError)
        {
            return built.Error;
        }

        var promptBudget = (budget is null ? null : await budget.For(ModelPurpose.Labeling, cancellationToken).ConfigureAwait(false)) ?? PromptBudget.Wide;
        var (candidates, skipped) = await Candidates(options, cancellationToken).ConfigureAwait(false);
        var generated = await new LabelGenerator(model, built.Value.Egress, promptBudget, candidates, scrubLabels: candidates > 1).Generate(clusters, maxTokens, cancellationToken).ConfigureAwait(false);
        if (generated.IsError)
        {
            return generated.Error;
        }

        // A pass that stopped early (a failed call, no model) has nothing to pick from; the pick step runs only on a whole generate step.
        return candidates > 1 && choice is not null && generated.Value is { Complete: true, Unavailable: null }
            ? await new LabelPicker(choice, built.Value.Egress).Pick(clusters, generated.Value.Named, (generated.Value.Spent, maxTokens), cancellationToken).ConfigureAwait(false)
            : Named(generated.Value, quiet: skipped && generated.Value is { Complete: true, Unavailable: null }, clusters.Count);
    }

    // The port is asked once a pass. With the pick on but no call able to go out (no key), candidates nobody can pick from would cost the model several times the tokens, so it names one label per cluster.
    private async ValueTask<(int Candidates, bool Skipped)> Candidates(LabelOptions? options, CancellationToken cancellationToken)
    {
        if (choice is null || options is not { Pick: true })
        {
            return (1, false);
        }

        return await choice.Unavailable(ChoiceKind.Labeling, cancellationToken).ConfigureAwait(false) is null ? (options.Candidates, false) : (1, true);
    }

    // A pick that could not be asked reports as it always did: how each label was chosen, and no degradation ('off' and 'no-key' are quiet).
    private static LabelPass Named(Generated generated, bool quiet, int clusterCount)
    {
        var labels = generated.Named.Select(static n => new ClusterLabel(n.Cluster.Id, n.Labels[0])).ToList();
        return new(labels, generated.Complete, generated.Unavailable) { Spent = generated.Spent, Chosen = quiet ? new LabelHow(0, labels.Count, clusterCount - labels.Count) : null };
    }
}
