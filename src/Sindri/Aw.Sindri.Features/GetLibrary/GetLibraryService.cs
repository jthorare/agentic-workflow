using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetLibrary;

/// <summary>The library page's read (PR review page spec §6.1): the facts cut to the page's cap, with how fresh the views are.</summary>
/// <param name="facts">What the library holds.</param>
/// <param name="status">How fresh the views are.</param>
internal sealed class GetLibraryService(ILibraryPageFacts facts, IProjectorStatus status) : IService<GetLibraryRequest, GetLibraryResponse>
{
    public async ValueTask<Result<GetLibraryResponse>> Handle(GetLibraryRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var read = await facts.Read(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var page = LibraryPage.From(read.Value);
        return new GetLibraryResponse(status.Current.AsOf, page.Docs, page.WithErrors, page.Broken, page.NearMisses, page.UnreadableRoots, page.CappedText);
    }
}
