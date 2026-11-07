using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetLibraryDoc;

/// <summary>One design doc (PR review page spec §6.1): an id nobody the caller may read has is SND-LIB-404.</summary>
/// <param name="facts">The library.</param>
/// <param name="status">How fresh the views are.</param>
internal sealed class GetLibraryDocService(ILibraryDocFacts facts, IProjectorStatus status) : IService<GetLibraryDocRequest, GetLibraryDocResponse>
{
    public async ValueTask<Result<GetLibraryDocResponse>> Handle(GetLibraryDocRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var found = await facts.Find(request.Doc, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        return found.Value is { } doc
            ? new GetLibraryDocResponse(status.Current.AsOf, doc)
            : SindriErrors.LibraryDocNotFound.With($"No design doc '{request.Doc}'; `sindri library list` names them");
    }
}
