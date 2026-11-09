using Aw.Sindri.Core.NeedsYou;

namespace Aw.Sindri.Features.GetNeedsYou;

/// <summary>The human's rows that are read at request time and not folded from events: the setup rows (the repo step's failure and the MCP need). Every caller gets them, a session too.</summary>
/// <param name="setup">The setup rows that are computed (the repo step's failure, the 'setup:mcp' need).</param>
internal sealed class NeedsYouLiveRows(SetupNotices setup)
{
    /// <summary>The setup rows.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The rows; empty when a source can't be read.</returns>
    public async ValueTask<IReadOnlyList<NeedsYouRow>> Read(CancellationToken cancellationToken) =>
        [.. await SetupRows(cancellationToken).ConfigureAwait(false)];

    // The setup rows that are computed, not folded.
    private async ValueTask<IReadOnlyList<NeedsYouRow>> SetupRows(CancellationToken cancellationToken)
    {
        var repo = await setup.Repo(cancellationToken).ConfigureAwait(false);
        var mcp = await setup.Mcp(cancellationToken).ConfigureAwait(false);
        return [.. new[] { repo, mcp }.OfType<NeedsYouRow>()];
    }
}
