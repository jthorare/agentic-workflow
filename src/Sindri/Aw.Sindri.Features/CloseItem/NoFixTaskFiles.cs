using Aw.Kernel;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Features.CloseItem;

/// <summary>The fix-task files port until a file-backed adapter is registered: holds nothing, so closing a fix item has no task to remove.</summary>
internal sealed class NoFixTaskFiles : IFixTaskFiles
{
    public ValueTask<Result<Unit>> Add(string head, string item, int pr, CancellationToken cancellationToken) => ValueTask.FromResult<Result<Unit>>(Unit.Value);

    public ValueTask<Result<Unit>> Remove(string item, CancellationToken cancellationToken) => ValueTask.FromResult<Result<Unit>>(Unit.Value);
}
