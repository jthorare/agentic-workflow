using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The question first (<see cref="ContextPageViewModel"/>, continued): what in the box is a question and what is already an anchor.</summary>
internal sealed partial class ContextPageViewModel
{
    private static List<string> Words(string text) =>
        [.. text.Split([' ', ',', '\t', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal)];

    // The anchors to read: anchor-shaped text as it is; a question is asked first, and when it finds nothing the whole text is read as one anchor (a label),
    // with the question's suggestions beside it as choices, never as matches (#1699).
    // Null when a newer Submit took over, or when the control plane is down (its problem is drawn here).
    private async Task<(List<string> Anchors, bool FromQuestion, List<string> Suggested)?> Resolve(Uri api, List<string> words, int generation, CancellationToken cancellationToken)
    {
        if (AnchorShaped(words))
        {
            return (words, false, []);
        }

        var asked = await Ports.Api.QueryAnchors(api, AnchorText.Trim(), cancellationToken).ConfigureAwait(true);
        if (generation != _generation)
        {
            return null;
        }

        if (!asked.IsSuccess && string.Equals(asked.Error.Code, SindriUiErrors.Unreachable.Code, StringComparison.Ordinal))
        {
            Show(view: null, choices: []);
            Problem = asked.Error.Message;
            Refresh();
            return null;
        }

        // why a miss is one anchor: the whole text may be a node's label, and a question is never a list of anchors
        if (!asked.IsSuccess)
        {
            return ([AnchorText.Trim()], false, []);
        }

        return asked.Value.Anchors.Count > 0
            ? ([.. asked.Value.Anchors.Distinct(StringComparer.Ordinal)], true, [])
            : ([AnchorText.Trim()], false, [.. asked.Value.SuggestedAnchors.Distinct(StringComparer.Ordinal)]);
    }

    // Anchors are keys like surface:pane/Drop; every word of one has a colon. Anything else is a person's question.
    private static bool AnchorShaped(List<string> words) => words.TrueForAll(w => w.Contains(':', StringComparison.Ordinal));
}
