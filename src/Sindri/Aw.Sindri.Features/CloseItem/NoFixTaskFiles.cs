using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Features.CloseItem;

/// <summary>The fix-task files port until a file-backed adapter is registered: holds nothing, so closing a fix item has no task to remove.</summary>
internal sealed class NoFixTaskFiles : IFixTaskFiles
{
    public ValueTask<Result<bool>> Add(string head, ItemId item, int pr, CancellationToken cancellationToken) => ValueTask.FromResult<Result<bool>>(false);

    public ValueTask<Result<bool>> Remove(ItemId item, CancellationToken cancellationToken) => ValueTask.FromResult<Result<bool>>(false);
}
