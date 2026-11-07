using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Reviews;

/// <summary>A row's CI roll-up (PR review page spec §6.1).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ReviewCi>))]
public enum ReviewCi
{
    /// <summary>No run on the head, or nothing known yet.</summary>
    [JsonStringEnumMemberName("none")]
    None,

    /// <summary>Every run on the head completed without a failure.</summary>
    [JsonStringEnumMemberName("pass")]
    Pass,

    /// <summary>A run on the head failed.</summary>
    [JsonStringEnumMemberName("fail")]
    Fail,

    /// <summary>A run on the head has not completed.</summary>
    [JsonStringEnumMemberName("running")]
    Running,
}
