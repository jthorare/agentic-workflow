using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The question first (<see cref="ContextPageViewModel"/>, continued): what in the box is a question and what is already an anchor.</summary>
internal sealed partial class ContextPageViewModel
{
    private static List<string> Words(string text) =>
        [.. text.Split([' ', ',', '\t', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal)];

    // The anchors to read: anchor-shaped text as it is; a question is asked first, and when it finds nothing the words are the anchors.
    // Null when a newer Submit took over, or when the control plane is down (its problem is drawn here).
    private async Task<(List<string> Anchors, bool FromQuestion)?> Resolve(Uri api, List<string> words, int generation, CancellationToken cancellationToken)
    {
        if (AnchorShaped(words))
        {
            return (words, false);
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

        return asked.IsSuccess && FromQuestion(asked.Value) is { Count: > 0 } found ? (found, true) : (words, false);
    }

    // Anchors are keys like surface:pane/Drop; every word of one has a colon. Anything else is a person's question.
    private static bool AnchorShaped(List<string> words) => words.TrueForAll(w => w.Contains(':', StringComparison.Ordinal));

    // The anchors the question resolved to, or when it found none, the ones it suggests.
    private static List<string> FromQuestion(Api.Context.QueryAnchorsView view) =>
        [.. (view.Anchors.Count > 0 ? view.Anchors : view.SuggestedAnchors).Distinct(StringComparer.Ordinal)];
}
