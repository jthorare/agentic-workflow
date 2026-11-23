using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.SearchMessages;

/// <summary>A stub until M1f (messages spec §14): local search.</summary>
internal sealed class SearchMessagesService : IService<SearchMessagesRequest, SearchMessagesResponse>
{
    public ValueTask<Result<SearchMessagesResponse>> Handle(SearchMessagesRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("SearchMessages is not built yet: messages spec §14 phase M1f");
}
