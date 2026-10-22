using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>Accept and Reject on a suggestion, and Copy path: the node card's sends (a desk verb each, one click, no confirm).</summary>
internal sealed partial class NodeCardViewModel
{
    private string? _link;

    /// <summary>Whether the node has a suggestion waiting for a person.</summary>
    public bool CanReview => _link is not null;

    /// <summary>What the last Accept or Reject did: "Saved to the draft map." or empty.</summary>
    public string Status { get; private set; } = string.Empty;

    /// <summary>Accepts the suggestion.</summary>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>When the answer is shown.</returns>
    internal Task Accept(CancellationToken cancellationToken = default) => Decide(DeskVerb.Accept, cancellationToken);

    /// <summary>Rejects the suggestion.</summary>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>When the answer is shown.</returns>
    internal Task Reject(CancellationToken cancellationToken = default) => Decide(DeskVerb.Reject, cancellationToken);

    /// <summary>Puts the file's path on the clipboard.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>When the clipboard holds it.</returns>
    internal async Task CopyPath(CancellationToken cancellationToken = default)
    {
        if (_map is not null && _key is not null && _map.Node(_key)?.Layout.Path is { Length: > 0 } path)
        {
            _ = await Ports.Clipboard.SetText(path, cancellationToken).ConfigureAwait(true);
        }
    }

    private async Task Decide(DeskVerb verb, CancellationToken cancellationToken)
    {
        if (_link is not { } link || Ports.ApiUrl() is not { } api)
        {
            return;
        }

        Problem = Status = string.Empty;
        var sent = await Ports.Api.ReviewDecision(api, new ReviewDecisionRequest(verb, Link: link), cancellationToken).ConfigureAwait(true);
        if (sent.IsSuccess)
        {
            Status = Labels.ContextInspSaved;
        }
        else
        {
            Problem = sent.Error.Message;
        }

        OnChanged(nameof(Status));
        OnChanged(nameof(Problem));
        OnChanged(nameof(HasProblem));
    }

    // A new node starts with no answer shown; the link is the one the map says waits on it.
    private void ResetReview()
    {
        Status = string.Empty;
        _link = _map is not null && _key is not null ? _map.SuggestionLink(_key) : null;
        OnChanged(nameof(Status));
        OnChanged(nameof(CanReview));
    }
}
