using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Desktop;

/// <summary>What a desktop script step does; each is one UI driver verb (or, for launch, the start of the app).</summary>
[JsonConverter(typeof(StrictEnumConverter<DesktopAction>))]
public enum DesktopAction
{
    /// <summary>Starts the app on the seeded data and waits for its driver; only as the first step, and implied when a script does not begin with it.</summary>
    [JsonStringEnumMemberName("launch")]
    Launch,

    /// <summary>Clicks the element the target names.</summary>
    [JsonStringEnumMemberName("click")]
    Click,

    /// <summary>Types the value into the element the target names (the focused element when there is no target).</summary>
    [JsonStringEnumMemberName("type")]
    Type,

    /// <summary>Presses a key chord ('Enter', 'Ctrl+K') on the element the target names (the focused element when there is no target).</summary>
    [JsonStringEnumMemberName("key")]
    Key,

    /// <summary>Waits until the target is in the expected state.</summary>
    [JsonStringEnumMemberName("wait-for")]
    WaitFor,

    /// <summary>Saves the window (or the element the target names) as the screen's frame.</summary>
    [JsonStringEnumMemberName("screenshot")]
    Screenshot,
}
