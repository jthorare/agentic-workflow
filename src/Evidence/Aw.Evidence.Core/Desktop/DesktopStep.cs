using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Desktop;

/// <summary>One scripted step of a desktop screen, in the shape of a ui-evidence step: an action, the element it acts on and the state that must hold after it.</summary>
/// <param name="Action">What the step does.</param>
/// <param name="Target">The element (none for launch; optional for type, key and screenshot).</param>
/// <param name="ExpectedState">What must hold afterwards (required for wait-for); checked with the driver's wait-for.</param>
/// <param name="Value">The text a type step enters, or the chord a key step presses.</param>
/// <param name="Replace">Whether a type step replaces the element's text instead of appending.</param>
/// <param name="TimeoutMs">How long the expected state may take, in milliseconds; the driver's default when null.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DesktopStep(
    DesktopAction Action,
    DesktopTarget? Target = null,
    DesktopExpectedState? ExpectedState = null,
    string? Value = null,
    bool? Replace = null,
    int? TimeoutMs = null);
