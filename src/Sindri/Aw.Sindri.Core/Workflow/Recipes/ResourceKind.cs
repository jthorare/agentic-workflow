using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>A resource a wait state can wait for (spec §3.4).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ResourceKind>))]
public enum ResourceKind
{
    /// <summary>A heavy-job slot from Sindri's heavy-job lock port.</summary>
    [JsonStringEnumMemberName("heavy")]
    Heavy = 1,

    /// <summary>A session slot (the P7 stub).</summary>
    [JsonStringEnumMemberName("slot")]
    Slot = 2,
}
