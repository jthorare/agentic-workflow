using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Desktop;

/// <summary>
/// The repo's screens manifest (schema 'ui-evidence.desktop-screens/1'): every screen of the desktop app that has a design, with the script that
/// brings it up in the app, the HTML mockup to compare it with and what may differ. Unknown keys are refused.
/// </summary>
/// <param name="Schema">Always <see cref="SchemaName"/>.</param>
/// <param name="DesignRoot">The folder of the mockups, relative to the manifest's folder.</param>
/// <param name="Screens">The screens, in the order they are reported.</param>
/// <param name="Threshold">The default limit of differing pixels, in percent; the config's when null.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DesktopManifest(string Schema, string DesignRoot, IReadOnlyList<DesktopScreen> Screens, double? Threshold = null)
{
    /// <summary>The schema this build reads.</summary>
    public const string SchemaName = "ui-evidence.desktop-screens/1";
}
