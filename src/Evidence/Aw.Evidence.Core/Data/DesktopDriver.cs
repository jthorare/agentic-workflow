using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Data;

/// <summary>The UI driver's command line, for a repo that keeps aw-ui-driver somewhere other than PATH.</summary>
/// <param name="Command">The program and its leading arguments; a relative program is the repo's.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DesktopDriver(IReadOnlyList<string> Command);
