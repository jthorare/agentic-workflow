using Aw.Context.Core;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>Asks Linear who the profile's key belongs to: one 'viewer { id name }' call (projects page spec §4.2).</summary>
/// <param name="client">The Linear client.</param>
/// <param name="deadline">How long the call gets.</param>
internal sealed class LinearViewer(LinearClient client, TimeSpan deadline) : ISourceViewer
{
    /// <inheritdoc />
    public async ValueTask<Result<SourceViewer>> Viewer(CancellationToken cancellationToken)
    {
        var data = await client.Query<LinearViewerData>("Viewer", [], deadline, cancellationToken).ConfigureAwait(false);
        if (data.IsError)
        {
            return data.Error;
        }

        var viewer = data.Value.Viewer;
        return ExternalId.TryFrom(viewer.Id, out var id)
            ? new SourceViewer(id, string.IsNullOrWhiteSpace(viewer.Name) ? viewer.Id : viewer.Name)
            : ContextErrors.LinearUnexpectedReply.With("the viewer has no usable id");
    }
}
