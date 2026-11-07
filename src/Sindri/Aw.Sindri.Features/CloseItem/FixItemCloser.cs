using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Reviews;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Features.CloseItem;

/// <summary>Closes a fix item through the CloseItem service as the 'service:pr-fix' caller, then removes its fix task.</summary>
/// <param name="items">Reads the item's version.</param>
/// <param name="close">The CloseItem service.</param>
/// <param name="files">The fix-task files.</param>
/// <param name="callers">Makes the service caller current.</param>
/// <param name="logger">Says when an item could not be closed.</param>
internal sealed partial class FixItemCloser(IItemResolver items, IService<CloseItemRequest, CommandAccepted> close, IFixTaskFiles files, ICallerContextAccessor callers, ILogger<FixItemCloser> logger) : IFixItemCloser
{
    public async ValueTask<bool> Close(string item, CancellationToken cancellationToken)
    {
        if (!ItemRef.TryParse(item, out var reference) || reference.Id is null)
        {
            NotAnItem(item);
            return false;
        }

        var closed = await CloseDone(reference, cancellationToken).ConfigureAwait(false);
        var removed = await files.Remove(item, cancellationToken).ConfigureAwait(false);   // a task whose item can't be closed is stale too
        if (removed.IsError)
        {
            TaskNotRemoved(item, removed.Error.Code, removed.Error.Message);
        }

        return closed;
    }

    private async ValueTask<bool> CloseDone(ItemRef reference, CancellationToken cancellationToken)
    {
        using var entered = callers.EnterService("pr-fix");
        if (callers.Current is not { } caller)
        {
            return false;
        }

        var row = await items.Resolve(reference, caller, cancellationToken).ConfigureAwait(false);
        if (row.IsError)
        {
            NotClosed(reference, row.Error.Code, row.Error.Message);
            return false;
        }

        var done = await close.Handle(new CloseItemRequest(reference, CloseReason.Done, row.Value.Version), cancellationToken).ConfigureAwait(false);
        if (done.IsError)
        {
            NotClosed(reference, done.Error.Code, done.Error.Message);
        }

        return done.IsSuccess;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "The fix marker names {Item}, which is not an item id; nothing to close")]
    private partial void NotAnItem(string item);

    [LoggerMessage(Level = LogLevel.Information, Message = "The fix item {Item} was not closed: {Code} {Message}")]
    private partial void NotClosed(ItemRef item, string code, string message);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The fix task of {Item} could not be removed: {Code} {Message}")]
    private partial void TaskNotRemoved(string item, string code, string message);
}
