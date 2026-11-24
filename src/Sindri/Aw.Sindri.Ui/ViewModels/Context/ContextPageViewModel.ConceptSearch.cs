using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context;


/// <summary>The concept layer's query (<see cref="ContextPageViewModel"/>, continued).</summary>
internal sealed partial class ContextPageViewModel
{
    // A query at the concept layer: the concepts whose name, summary or terms hold the words, and the ones that hold a node the words (or the anchors the question
    // resolved to) name. The question's suggestions are choices, never matches (#1699).
    private async Task SearchConcepts(Uri api, List<string> words, int generation, CancellationToken cancellationToken)
    {
        var matches = new HashSet<string>(Concepts.Matching(AnchorText.Trim()), StringComparer.Ordinal);
        var resolved = await Resolve(api, words, generation, cancellationToken).ConfigureAwait(true);
        if (generation != _generation)
        {
            return;
        }

        Choices = [];
        if (resolved is { } found)
        {
            var located = await Ports.Api.WholeMap(api, new WholeMapQuery("areas", Area: null, [.. found.Anchors.Take(MaxLocate)], MapVersion: null, Draft: false, Repo: _repo), cancellationToken).ConfigureAwait(true);
            if (generation != _generation)
            {
                return;
            }

            if (located.IsSuccess)
            {
                matches.UnionWith((located.Value.Located ?? []).SelectMany(l => l.Concept ?? []));
            }
        }

        Concepts.ApplySearch(matches);
        if (matches.Count == 0 && resolved is { Suggested.Count: > 0 } missed)
        {
            Choices = missed.Suggested;
        }

        ChangedConcepts();
        Refresh();
    }

    private void ChangedConcepts()
    {
        OnChanged(nameof(ShowConcepts));
        OnChanged(nameof(KeyHints));
        OnChanged(nameof(ShowWhole));
        OnChanged(nameof(ShowWholeStatus));
        OnChanged(nameof(OpenedConceptId));
        OnChanged(nameof(Head));
        OnChanged(nameof(HasHead));
        OnChanged(nameof(HeadDetail));
        OnChanged(nameof(SearchNote));
        OnChanged(nameof(HasSearchNote));
        ChangedNoConcept();
    }
}
