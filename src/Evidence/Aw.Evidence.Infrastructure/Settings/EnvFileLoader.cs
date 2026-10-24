using System.Text.RegularExpressions;

namespace Aw.Evidence.Infrastructure.Settings;

/// <summary>
/// Layers the deployment's env files under the process environment for run, parity and doctor, as the TypeScript runner and doctor.sh did.
/// The files are plain NAME=value lines (comments, an optional export  prefix, one layer of quotes) that are parsed, never sourced, so a
/// value is never executed or expanded. Only the UI_EVIDENCE_* settings of the skill's Configuration section are taken from a file; any other
/// name in it is ignored, so a file can't redirect the state directory, the locks or the loader. A name set in the environment is never
/// overridden (blank counts as unset), and the first file that sets a name wins over the files after it (the local file, then the
/// committed deployment.env).
/// </summary>
internal static partial class EnvFileLoader
{
    internal const string DirectoryVariable = "UI_EVIDENCE_ENV_DIR";
    internal const string ConfigVariable = "UI_EVIDENCE_CONFIG";
    internal const string LocalFileName = ".ui-evidence.local.env";
    internal const string DeploymentFileName = "deployment.env";

    // UI_EVIDENCE_CONFIG and UI_EVIDENCE_ENV_DIR are not here: a file can't name the files that are read.
    private static readonly HashSet<string> _allowed = new(StringComparer.Ordinal)
    {
        "UI_EVIDENCE_USER_AGENT", "UI_EVIDENCE_APP_TITLE", "UI_EVIDENCE_PREVIEW_HOST_PATTERN", "UI_EVIDENCE_API_PORT",
        "UI_EVIDENCE_DB_CONTAINER", "UI_EVIDENCE_DB_USER", "UI_EVIDENCE_SEED_TABLE", "UI_EVIDENCE_SEED_DOMAINS", "UI_EVIDENCE_SEED_EMAIL_PATTERN",
        "UI_EVIDENCE_SLOT_FILE", "UI_EVIDENCE_SLOT_KEY", "UI_EVIDENCE_SLOT_COUNT", "UI_EVIDENCE_SLOT_PORT_STEP", "UI_EVIDENCE_SLOT_DATABASE",
    };

    /// <summary>The environment as the settings see it: the process environment, then <paramref name="files"/> in order for the names they may set.</summary>
    /// <param name="environment">Reads a process environment variable.</param>
    /// <param name="files">The env file paths, highest precedence first.</param>
    /// <param name="readFile">Reads a file's text, or null when it is absent or unreadable (skipped, not an error).</param>
    /// <returns>The layered lookup.</returns>
    internal static Func<string, string?> Overlay(Func<string, string?> environment, IReadOnlyList<string> files, Func<string, string?> readFile)
    {
        var parsed = files.Select(file => new Lazy<IReadOnlyDictionary<string, string>>(() => readFile(file) is { } text ? Parse(text) : new Dictionary<string, string>(StringComparer.Ordinal))).ToList();
        return name =>
        {
            var value = environment(name);
            if (!string.IsNullOrWhiteSpace(value) || !_allowed.Contains(name))
            {
                return value;
            }

            foreach (var file in parsed)
            {
                if (file.Value.TryGetValue(name, out var fromFile) && !string.IsNullOrWhiteSpace(fromFile))
                {
                    return fromFile;
                }
            }

            return value;
        };
    }

    /// <summary>The env files for this process, highest precedence first: the local file (UI_EVIDENCE_CONFIG, else the default one), then deployment.env.</summary>
    /// <param name="environment">Reads a process environment variable.</param>
    /// <param name="workingDirectory">The current directory; its .agentic-workflow/ui-evidence is the default directory.</param>
    /// <returns>The paths.</returns>
    internal static IReadOnlyList<string> Files(Func<string, string?> environment, string workingDirectory)
    {
        var directory = environment(DirectoryVariable) is { } set && !string.IsNullOrWhiteSpace(set)
            ? set
            : Path.Combine(workingDirectory, ".agentic-workflow", "ui-evidence");
        var local = environment(ConfigVariable) is { } config && !string.IsNullOrWhiteSpace(config) ? config : Path.Combine(directory, LocalFileName);
        return [local, Path.Combine(directory, DeploymentFileName)];
    }

    /// <summary>Reads a file as text, or null when it is absent or unreadable.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The text, or null.</returns>
    internal static string? ReadFile(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static Dictionary<string, string> Parse(string text)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in text.Split('\n'))
        {
            if (Line.Match(raw.Trim()) is { Success: true } line && !values.ContainsKey(line.Groups["name"].Value))
            {
                values[line.Groups["name"].Value] = Unquote(line.Groups["value"].Value.Trim());
            }
        }

        return values;
    }

    private static string Unquote(string value) =>
        value.Length >= 2 && (value[0] is '"' or '\'') && value[^1] == value[0] ? value[1..^1] : value;

    [GeneratedRegex("^(?:export\\s+)?(?<name>[A-Za-z_][A-Za-z0-9_]*)=(?<value>.*)$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex Line { get; }
}
