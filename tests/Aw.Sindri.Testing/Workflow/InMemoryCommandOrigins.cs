using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>
/// The in-memory <see cref="ICommandOrigins"/> over the in-memory ledger: the causing event, by envelope id, names its
/// instance in its payload's 'instance' field. Passes <see cref="CommandOriginsContract"/>.
/// </summary>
/// <param name="ledger">The in-memory ledger it reads.</param>
public sealed class InMemoryCommandOrigins(InMemorySindriStore ledger) : ICommandOrigins
{
    /// <inheritdoc/>
    public ValueTask<Result<InstanceAddress?>> CauserOf(LedgerEvent reply, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reply);
        var found = reply.Envelope.CausationId is { } command ? ledger.Events.FirstOrDefault(e => e.Envelope.Id == command) : null;
        InstanceAddress? instance = null;
        if (found is not null)
        {
            using var payload = JsonDocument.Parse(found.Envelope.Payload);
            if (payload.RootElement.ValueKind == JsonValueKind.Object
                && payload.RootElement.TryGetProperty("instance", out var named)
                && named.ValueKind == JsonValueKind.String
                && InstanceAddress.TryFrom(named.GetString(), out var address))
            {
                instance = address;
            }
        }

        return ValueTask.FromResult<Result<InstanceAddress?>>(instance);
    }
}
