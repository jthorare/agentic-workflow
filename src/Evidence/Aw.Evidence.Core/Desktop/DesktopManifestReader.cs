using System.Text.Json;
using Aw.Kernel;

namespace Aw.Evidence.Core.Desktop;

/// <summary>Reads a screens manifest and checks its rules.</summary>
public static class DesktopManifestReader
{
    /// <summary>Parses and validates a screens manifest.</summary>
    /// <param name="utf8Json">The file's bytes.</param>
    /// <param name="options">The package's AwJson options.</param>
    /// <returns>The manifest, or AW-EVIDENCE-026 naming every problem.</returns>
    public static Result<DesktopManifest> Read(ReadOnlySpan<byte> utf8Json, JsonSerializerOptions options) =>
        JsonInput.Read<DesktopManifest>(utf8Json, options, EvidenceErrors.DesktopManifestInvalid).Bind(Validate);

    private static Result<DesktopManifest> Validate(DesktopManifest manifest)
    {
        var problems = new Problems();
        problems.AddIf(manifest.Schema != DesktopManifest.SchemaName, $"$.schema: must be {DesktopManifest.SchemaName}");
        problems.AddIf(string.IsNullOrWhiteSpace(manifest.DesignRoot) || manifest.DesignRoot.StartsWith('/'), "$.designRoot: must be a folder relative to the manifest's folder ('.' for the folder itself)");
        problems.AddIf(manifest.Threshold is < 0 or > 100, "$.threshold: must be 0 to 100");
        problems.AddIf(manifest.Screens is null || manifest.Screens.Count == 0, "$.screens: must list at least one screen");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var screen in (manifest.Screens ?? []).Where(static s => s is not null))
        {
            problems.AddIf(!seen.Add(screen.Name ?? string.Empty), $"$.screens: the name '{screen.Name}' is used twice");
            DesktopScreenRules.Check(screen, problems);
        }

        problems.AddIf((manifest.Screens ?? []).Any(static s => s is null), "$.screens: holds a null entry");
        return problems.Into(manifest, EvidenceErrors.DesktopManifestInvalid);
    }
}
