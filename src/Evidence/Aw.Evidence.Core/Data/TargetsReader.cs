using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aw.Kernel;

namespace Aw.Evidence.Core.Data;

/// <summary>Reads a targets config document and checks its rules.</summary>
public static partial class TargetsReader
{
    /// <summary>Parses and validates a targets file.</summary>
    /// <param name="utf8Json">The file's bytes.</param>
    /// <param name="options">The package's AwJson options.</param>
    /// <returns>The targets, or AW-EVIDENCE-022 naming the JSON location and every problem.</returns>
    public static Result<ResolvedTargets> Read(ReadOnlySpan<byte> utf8Json, JsonSerializerOptions options) =>
        JsonInput.Read<TargetsFile>(utf8Json, options, EvidenceErrors.TargetsInvalid).Bind(Validate);

    private static Result<ResolvedTargets> Validate(TargetsFile file)
    {
        var problems = new Problems();
        problems.AddIf(file.Schema is not null && !TargetsFile.SchemaNames.Contains(file.Schema, StringComparer.Ordinal), "$.schema: must be evidence.targets/1 or evidence.repo/1 when given");
        var outputs = BuildOutputs(file.Cli?.BuildOutputs ?? [], problems);
        var allow = file.Cli?.Allow ?? [];
        for (var i = 0; i < allow.Count; i++)
        {
            problems.AddIf(!allow[i].StartsWith('/'), $"{Item("$.cli.allow", i)}: must be an absolute path");
        }

        var passEnv = file.Cli?.PassEnv ?? [];
        for (var i = 0; i < passEnv.Count; i++)
        {
            problems.AddIf(!EnvName.IsMatch(passEnv[i]), $"{Item("$.cli.passEnv", i)}: must be an UPPER_SNAKE environment variable name");
        }

        var databases = CheckDatabases(file.Databases, problems);
        var desktop = DesktopTargetsRules.Resolve(file.Desktop, problems);
        return problems.Into(new ResolvedTargets(outputs, [.. allow], [.. passEnv], databases, desktop), EvidenceErrors.TargetsInvalid);
    }

    private static Dictionary<string, DatabaseEntry> CheckDatabases(IReadOnlyDictionary<string, DatabaseEntry>? raw, Problems problems)
    {
        var databases = new Dictionary<string, DatabaseEntry>(StringComparer.Ordinal);
        foreach (var (name, entry) in raw ?? new Dictionary<string, DatabaseEntry>(StringComparer.Ordinal))
        {
            var at = "$.databases." + name;
            problems.AddIf(name.Length == 0, "$.databases: a name must not be empty");
            problems.AddIf(!string.Equals(entry.Engine, DatabaseEntry.Postgres, StringComparison.Ordinal), $"{at}.engine: must be postgres");
            problems.AddIf(string.IsNullOrWhiteSpace(entry.Host) || entry.Host.StartsWith('/') || entry.Host.Any(char.IsWhiteSpace), $"{at}.host: must be a TCP host name or address (a Unix socket directory is not supported)");
            problems.AddIf(entry.Port is < 1 or > 65535, $"{at}.port: must be 1 to 65535");
            problems.AddIf(string.IsNullOrWhiteSpace(entry.Database), $"{at}.database: must not be empty");
            problems.AddIf(string.IsNullOrWhiteSpace(entry.User), $"{at}.user: must not be empty");
            problems.AddIf(!EnvName.IsMatch(entry.PasswordEnv), $"{at}.passwordEnv: must name an UPPER_SNAKE environment variable");
            problems.AddIf(string.IsNullOrWhiteSpace(entry.Identity.ServerAddress), $"{at}.identity.serverAddress: must not be empty");
            problems.AddIf(string.IsNullOrWhiteSpace(entry.Identity.Database), $"{at}.identity.database: must not be empty");
            problems.AddIf(entry.Identity.ServerPort is < 1 or > 65535, $"{at}.identity.serverPort: must be 1 to 65535 when given");
            databases[name] = entry;
        }

        return databases;
    }

    private static List<string> BuildOutputs(IReadOnlyList<string> raw, Problems problems)
    {
        var outputs = new List<string>();
        for (var i = 0; i < raw.Count; i++)
        {
            var at = Item("$.cli.buildOutputs", i);
            if (raw[i].Length == 0 || raw[i].StartsWith('/'))
            {
                problems.Add($"{at}: must be a relative directory below the repo");
            }
            else if (Normalize(raw[i]) is not { Length: > 0 } normalized)
            {
                problems.Add($"{at}: must stay inside the repo and name a directory below it");
            }
            else
            {
                outputs.Add(normalized);
            }
        }

        return outputs;
    }

    // The relative path without '.' segments and with '..' folded in; null when it climbs out of the repo or names the repo itself.
    private static string? Normalize(string relative)
    {
        var parts = new List<string>();
        foreach (var segment in relative.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == "..")
            {
                if (parts.Count == 0)
                {
                    return null;
                }

                parts.RemoveAt(parts.Count - 1);
            }
            else if (segment != ".")
            {
                parts.Add(segment);
            }
        }

        return string.Join('/', parts);
    }

    private static string Item(string list, int index) => list + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";

    [GeneratedRegex("^[A-Z][A-Z0-9_]*\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex EnvName { get; }
}
