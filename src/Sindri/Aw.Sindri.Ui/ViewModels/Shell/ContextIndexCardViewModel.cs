using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>
/// Health's Context index card (#1597): one line per context source from GET /sindri/index/status. Followed like the other views: read on
/// connect and after every event, never folded.
/// </summary>
/// <param name="api">The API.</param>
/// <param name="clock">For the age of the last commit and the minutes to a retry.</param>
internal sealed class ContextIndexCardViewModel(ISindriApi api, TimeProvider clock) : ObservableObject
{
    /// <summary>The state in a word at the heading's right: "ok" or "needs a look"; empty with no lines.</summary>
    public string Word { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The lines, one per source.</summary>
    public IReadOnlyList<CodeIndexLine> Lines { get; private set => Set(ref field, value); } = [];

    /// <summary>Reads the status and shows it.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal async Task Refresh(Uri url, CancellationToken cancellationToken)
    {
        _ = await api.IndexStatus(url, cancellationToken).ConfigureAwait(true);
        _ = clock.GetUtcNow();
        throw new NotSupportedException();
    }
}
