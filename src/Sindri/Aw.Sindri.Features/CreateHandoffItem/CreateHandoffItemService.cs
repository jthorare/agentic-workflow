using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.CreateHandoffItem;

/// <summary>Makes an item from a handoff (#2258 PR 12): the existing create-item handler, then 'item.handoff' with the whole payload.</summary>
internal sealed class CreateHandoffItemService : IService<CreateHandoffItemRequest, HandoffItemCreatedResponse>
{
    public ValueTask<Result<HandoffItemCreatedResponse>> Handle(CreateHandoffItemRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("CreateHandoffItem is not built yet");
}
