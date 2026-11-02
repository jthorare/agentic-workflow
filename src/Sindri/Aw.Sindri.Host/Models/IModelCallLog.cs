using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Host.Models;

/// <summary>Where the router writes its 'context.model.called' event (model routing spec §4 "Writing events"). Best effort: a failure is the writer's to log, never the caller's.</summary>
internal interface IModelCallLog
{
    /// <summary>Records one call.</summary>
    /// <param name="called">The event payload.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the event is written or the write has failed and been logged.</returns>
    public ValueTask Record(ContextModelCalled called, CancellationToken cancellationToken);
}
