using Aw.Context.Core.Pages;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>One slice of the pages of the repo the caller may read, through the engine (which gates and scopes sessions).</summary>
internal sealed class GetContextPagesService : IService<GetContextPagesRequest, PagesView>
{
    public ValueTask<Result<PagesView>> Handle(GetContextPagesRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("The pages read is not built yet.");
}
