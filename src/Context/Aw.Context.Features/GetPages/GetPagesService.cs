using Aw.Context.Core.Pages;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.GetPages;

/// <summary>One slice of the pages of the map the caller may read, with their actions and concepts.</summary>
internal sealed class GetPagesService : IService<GetPagesRequest, PagesView>
{
    public ValueTask<Result<PagesView>> Handle(GetPagesRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("The pages read is not built yet.");
}
