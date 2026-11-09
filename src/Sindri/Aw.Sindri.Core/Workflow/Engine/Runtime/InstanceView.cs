using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Instances;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// What the instance reads return for one workflow instance: where it is and what the launcher needs of it. The recipe engine that rendered the full status
/// (state chips, desk forms, allowed commands) is gone; the sweeps and reads that still ask an instance for its state read this.
/// </summary>
/// <param name="Address">The instance address (its workflow id).</param>
/// <param name="Recipe">The recipe it runs.</param>
/// <param name="State">The state it is in, or 'parked', 'escalated', 'taken-over'.</param>
/// <param name="Since">When the instance entered <see cref="State"/>.</param>
public sealed record InstanceView(InstanceAddress Address, RecipeVersion Recipe, string State, DateTimeOffset Since)
{
    /// <summary>The instance's opaque id, minted from <see cref="Address"/> with <see cref="WorkflowInstanceId.Of"/>. Required, so a view read back from JSON always carries it.</summary>
    public required WorkflowInstanceId Id { get; init; }

    /// <summary>The count of events the instance has mirrored into the ledger.</summary>
    public int Version { get; init; }

    /// <summary>Who took the instance over; null when it is not taken over.</summary>
    public string? TakenOverBy { get; init; }

    /// <summary>The open agent run, when one is running.</summary>
    public OpenRun? AgentRun { get; init; }

    /// <summary>The item the instance works on.</summary>
    public ItemKey? ItemId { get; init; }

    /// <summary>The stored facts by key, as their content hashes.</summary>
    public IReadOnlyDictionary<string, string>? FactShas { get; init; }
}
