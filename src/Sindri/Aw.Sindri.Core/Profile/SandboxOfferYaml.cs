using System.Text;
using System.Text.RegularExpressions;

namespace Aw.Sindri.Core.Profile;

/// <summary>Writes an offered 'sandbox.repos.&lt;repo&gt;' entry as the YAML to paste into the profile.</summary>
public static partial class SandboxOfferYaml
{
    /// <summary>Renders the snippet.</summary>
    /// <param name="repo">The repo's name in the profile.</param>
    /// <param name="settings">The offered entries.</param>
    /// <returns>The lines of a 'sandbox:' section holding only this repo's entry, each ending in a line break.</returns>
    public static string Render(string repo, SandboxRepoSettings settings)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(settings);
        var text = new StringBuilder("sandbox:\n  repos:\n").Append("    ").Append(Scalar(repo)).Append(":\n");
        Flow(text, "groups", settings.Groups);
        Flow(text, "hosts", settings.Hosts);
        Flow(text, "cachePaths", settings.CachePaths);
        if (settings.Prepare?.Dirs is { Count: > 0 } dirs)
        {
            _ = text.Append("      prepare:\n        dirs:\n");
            foreach (var dir in dirs)
            {
                _ = text.Append("          - dir: ").Append(Scalar(dir.Dir ?? ".")).Append('\n');
                _ = dir.Tool is { } tool ? text.Append("            tool: ").Append(Scalar(tool)).Append('\n') : text;
            }
        }

        return text.ToString();
    }

    private static void Flow(StringBuilder text, string key, IReadOnlyList<string>? values)
    {
        if (values is { Count: > 0 })
        {
            _ = text.Append("      ").Append(key).Append(": [").AppendJoin(", ", values.Select(Scalar)).Append("]\n");
        }
    }

    private static string Scalar(string value) =>
        Plain.IsMatch(value) && !Reserved.IsMatch(value) ? value : "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    [GeneratedRegex("^(true|false|null|yes|no|on|off|[0-9.+-]+)\\z", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex Reserved { get; }

    [GeneratedRegex("^[A-Za-z0-9._/@+][A-Za-z0-9._/@+ -]*\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex Plain { get; }
}
