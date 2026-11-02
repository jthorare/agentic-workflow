using Aw.Sindri.Core.Events;
using Aw.Sindri.Host.Models;

namespace Aw.Sindri.Tests.ContextCalls.Routing;

/// <summary>Keeps every event the router writes.</summary>
internal sealed class RecordedCalls : IModelCallLog
{
    public List<ContextModelCalled> Calls { get; } = [];

    public ValueTask Record(ContextModelCalled called, CancellationToken cancellationToken)
    {
        lock (Calls)
        {
            Calls.Add(called);
        }

        return ValueTask.CompletedTask;
    }
}
