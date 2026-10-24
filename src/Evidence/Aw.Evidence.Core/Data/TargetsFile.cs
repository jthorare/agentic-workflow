using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Data;

/// <summary>The repo's targets config file (.agentic-workflow/evidence.json, or the file --config names).</summary>
/// <param name="Schema">'evidence.targets/1' or 'evidence.repo/1' when given.</param>
/// <param name="Cli">Where programs live and which may run.</param>
/// <param name="Databases">The databases db-run may read; each entry is checked when the file is read.</param>
/// <param name="Desktop">What desktop-run needs: the seeded-data, app and driver commands and the screens manifest.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TargetsFile(string? Schema = null, CliTargets? Cli = null, IReadOnlyDictionary<string, DatabaseEntry>? Databases = null, DesktopTargets? Desktop = null)
{
    /// <summary>The schema names a targets file may state.</summary>
    public static IReadOnlyList<string> SchemaNames { get; } = ["evidence.targets/1", "evidence.repo/1"];

    /// <summary>The file's place below the repo.</summary>
    public const string RepoRelativePath = ".agentic-workflow/evidence.json";
}
