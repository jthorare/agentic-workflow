using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Scripts;

/// <summary>What a script step does.</summary>
[JsonConverter(typeof(StrictEnumConverter<StepAction>))]
public enum StepAction
{
    /// <summary>Navigate to a path on the host.</summary>
    [JsonStringEnumMemberName("goto")]
    Goto,

    /// <summary>Click the element the target names (a test id, 'role:&lt;role&gt;|&lt;name&gt;' or 'text:&lt;text&gt;').</summary>
    [JsonStringEnumMemberName("click")]
    Click,

    /// <summary>Fill the element with the target test id.</summary>
    [JsonStringEnumMemberName("fill")]
    Fill,

    /// <summary>Wait for the target text to be visible.</summary>
    [JsonStringEnumMemberName("expect-visible")]
    ExpectVisible,

    /// <summary>Scroll the target test id's container (read-only); the value is 'end' (default), 'start', 'bottom', 'top' or a horizontal pixel offset.</summary>
    [JsonStringEnumMemberName("scroll")]
    Scroll,

    /// <summary>Drag the target test id to the destination test id with real mouse events.</summary>
    [JsonStringEnumMemberName("drag")]
    Drag,

    /// <summary>Press a key on the target (focused first), or on whatever has focus when there is no target.</summary>
    [JsonStringEnumMemberName("press")]
    Press,
}
