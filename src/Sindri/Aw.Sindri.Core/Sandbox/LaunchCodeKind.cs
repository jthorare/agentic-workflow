using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Sandbox;

/// <summary>What a sandbox launch code means to the engine (sandbox spec SB11): the engine routes on the code, never on text.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LaunchCodeKind>))]
public enum LaunchCodeKind
{
    /// <summary>The Step has no agent runtime on this box right now (-014, -017): a deskCapable Step takes the runtime.unavailable transition, any other parks NoRuntime.</summary>
    [JsonStringEnumMemberName("no-runtime")]
    NoRuntime,

    /// <summary>A 'launch' park whose Resume starts a new run (-015, -016).</summary>
    [JsonStringEnumMemberName("launch-park")]
    LaunchPark,

    /// <summary>The Step fails (-018, -019, -021); never auto-retried in the same run.</summary>
    [JsonStringEnumMemberName("step-failure")]
    StepFailure,

    /// <summary>A queue condition, not a fault (-020): the launch activity retries with backoff.</summary>
    [JsonStringEnumMemberName("resource-wait")]
    ResourceWait,
}
