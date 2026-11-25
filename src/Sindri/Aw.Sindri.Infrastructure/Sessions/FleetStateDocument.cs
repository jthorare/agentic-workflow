using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Sessions;

/// <summary>The aw:fleet hook's state file (schemas/fleet-state.schema.json): unknown keys are errors, so a 'tool_input' or a notification 'message' never loads.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record FleetStateDocument(int Schema, string Provider, string SessionId, string Event, string State, DateTimeOffset At)
{
    public string? Reason { get; init; }

    public string? Tool { get; init; }

    public DateTimeOffset? ToolAt { get; init; }

    public string? Cwd { get; init; }

    public string? Transcript { get; init; }

    public int? AgentPid { get; init; }

    public long? AgentStartTicks { get; init; }

    public Dictionary<string, string>? Env { get; init; }
}
