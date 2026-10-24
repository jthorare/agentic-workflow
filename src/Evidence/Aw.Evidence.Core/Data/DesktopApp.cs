using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Data;

/// <summary>The command that starts the repo's desktop app (it must opt into the UI driver, which AW_UI_DRIVER=1 switches on).</summary>
/// <param name="Command">The program and its arguments; a relative program is the repo's. It runs with the fixture's home variable and the driver's variables set.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DesktopApp(IReadOnlyList<string> Command);
