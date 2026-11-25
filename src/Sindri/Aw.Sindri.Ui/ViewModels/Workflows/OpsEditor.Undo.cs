using System.Text.Json;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The workflow editor's logic (<see cref="OpsEditor"/>, continued): the graph as last read, and putting an older one back as a new revision.</summary>
internal sealed partial class OpsEditor
{
    /// <summary>The graph of the revision the form was made from: what Undo puts back after the next save.</summary>
    internal JsonElement SpecRead { get; private set; }

    private static JsonElement Kept(JsonElement spec) => spec.ValueKind == JsonValueKind.Undefined ? spec : spec.Clone();   // a copy outlives the answer's document

    /// <summary>Saves an older graph as a new revision on the revision read; never a rewind. A stale base holds further edits and asks for a reload, like any save.</summary>
    /// <param name="slug">The workflow the graph belongs to; another one open now is refused.</param>
    /// <param name="spec">The graph to put back.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Null when it was saved, else the reason it was not.</returns>
    internal async Task<string?> Restore(string slug, JsonElement spec, CancellationToken cancellationToken)
    {
        if (IsSending || NeedsReload || _url is not { } url || !string.Equals(Slug, slug, StringComparison.Ordinal))
        {
            return Labels.WfUndoNotNow;
        }

        (FailureText, _refused) = (string.Empty, false);
        IsSending = true;
        try
        {
            var replaced = await api.ReplaceWorkflow(url, Slug, BaseRevision, spec.GetRawText(), cancellationToken).ConfigureAwait(true);
            if (replaced.IsError)
            {
                Refused(replaced.Error);
                return NeedsReload ? Labels.WfUndoNotNow : replaced.Error.Message;
            }

            await Load(url, Slug, afterSave: true, cancellationToken).ConfigureAwait(true);
            return null;
        }
        finally
        {
            IsSending = false;
        }
    }
}
