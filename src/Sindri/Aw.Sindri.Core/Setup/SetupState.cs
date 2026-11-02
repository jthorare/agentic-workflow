using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Setup;

/// <summary>Where a setup step stands; written in kebab case.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SetupState>))]
public enum SetupState
{
    /// <summary>Not started.</summary>
    [JsonStringEnumMemberName("todo")]
    Todo,

    /// <summary>Under way in the background.</summary>
    [JsonStringEnumMemberName("running")]
    Running,

    /// <summary>Finished.</summary>
    [JsonStringEnumMemberName("done")]
    Done,

    /// <summary>It didn't work, or its state couldn't be read.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,

    /// <summary>Waiting for the human.</summary>
    [JsonStringEnumMemberName("needs-you")]
    NeedsYou,

    /// <summary>No step is skipped today; a step that can't run on this machine would be, and it counts as finished.</summary>
    [JsonStringEnumMemberName("skipped")]
    Skipped,
}
