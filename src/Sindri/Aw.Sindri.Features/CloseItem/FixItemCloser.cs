using Aw.Kernel;
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
        if (!ItemRef.TryParse(item, out var reference) || reference.Id is not { } id)
        {
            NotAnItem(item);
            return false;
        }

        using var entered = callers.EnterService("pr-fix");
        if (callers.Current is not { } caller)
        {
            return false;
        }

        var outcome = await Attempt(reference, caller, cancellationToken).ConfigureAwait(false);
        if (outcome == Outcome.Conflict)
        {
            outcome = await Attempt(reference, caller, cancellationToken).ConfigureAwait(false);   // the item moved under us: read it again and try once more
        }

        if (outcome is Outcome.Closed or Outcome.Gone)
        {
            var removed = await files.Remove(id, cancellationToken).ConfigureAwait(false);   // closed, unknown or already closed: the task is stale; a failed close keeps it
            if (removed.IsError)
            {
                TaskNotRemoved(item, removed.Error.Code, removed.Error.Message);
            }
        }

        return outcome == Outcome.Closed;
    }

    private async ValueTask<Outcome> Attempt(ItemRef reference, CallerContext caller, CancellationToken cancellationToken)
    {
        var row = await items.Resolve(reference, caller, cancellationToken).ConfigureAwait(false);
        if (row.IsError)
        {
            NotClosed(reference, row.Error.Code, row.Error.Message);
            return row.Error.Kind == ErrorKind.NotFound ? Outcome.Gone : Outcome.Failed;
        }

        if (ItemStates.IsClosed(row.Value.State))
        {
            return Outcome.Gone;
        }

        var done = await close.Handle(new CloseItemRequest(reference, CloseReason.Done, row.Value.Version), cancellationToken).ConfigureAwait(false);
        if (done.IsSuccess)
        {
            return Outcome.Closed;
        }

        NotClosed(reference, done.Error.Code, done.Error.Message);
        return done.Error.Kind == ErrorKind.Conflict ? Outcome.Conflict : Outcome.Failed;
    }

    private enum Outcome
    {
        Closed,
        Gone,
        Conflict,
        Failed,
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "The fix marker names {Item}, which is not an item id; nothing to close")]
    private partial void NotAnItem(string item);

    [LoggerMessage(Level = LogLevel.Information, Message = "The fix item {Item} was not closed: {Code} {Message}")]
    private partial void NotClosed(ItemRef item, string code, string message);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The fix task of {Item} could not be removed: {Code} {Message}")]
    private partial void TaskNotRemoved(string item, string code, string message);
}
