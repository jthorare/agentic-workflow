using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Onyx;

/// <summary>Where the managed Onyx stands; written in kebab case.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<OnyxState>))]
public enum OnyxState
{
    /// <summary>Nothing has been set up yet.</summary>
    [JsonStringEnumMemberName("not-set-up")]
    NotSetUp,

    /// <summary>Set up, and the web server and API server containers are running.</summary>
    [JsonStringEnumMemberName("running")]
    Running,

    /// <summary>Set up, and not running (stopped by `onyx down`, or Docker isn't answering).</summary>
    [JsonStringEnumMemberName("stopped")]
    Stopped,

    /// <summary>Set up, but Docker isn't usable here (a remote endpoint, no daemon, no CLI), so whether it runs can't be told.</summary>
    [JsonStringEnumMemberName("unknown")]
    Unknown,
}
