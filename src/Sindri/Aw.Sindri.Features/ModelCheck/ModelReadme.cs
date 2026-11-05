using System.Globalization;

namespace Aw.Sindri.Features.ModelCheck;

/// <summary>
/// What a spec folder's README.md tells the step (model-check design §3, "The README"): the code it models (line 1, 'models: path[:line] ...'), the bounds in plain words, one line per property and the
/// variables' words. A README without a line leaves it empty and the step falls back, with an MDL-SPEC-002 warning.
/// </summary>
/// <param name="Models">The modelled paths, repo-relative, without their ':line'; null when line 1 is not a 'models:' line (the folder is then always checked).</param>
/// <param name="Bounds">The text after 'Bounds:', or null.</param>
/// <param name="Properties">Property name to its plain words, from the '- Name: words' lines.</param>
/// <param name="Vars">Variable name to its words, from the 'Vars:' line (entries separated by '·').</param>
internal sealed record ModelReadme(IReadOnlyList<string>? Models, string? Bounds, IReadOnlyDictionary<string, string> Properties, IReadOnlyDictionary<string, string> Vars)
{
    private const string ModelsPrefix = "models:";

    /// <summary>Reads a README; an empty or odd text gives a README with nothing in it.</summary>
    /// <param name="text">The README's text.</param>
    /// <returns>What it says.</returns>
    internal static ModelReadme Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = text.Split('\n').Select(static l => l.TrimEnd('\r')).ToList();
        IReadOnlyList<string>? models = null;
        if (lines.Count > 0 && lines[0].StartsWith(ModelsPrefix, StringComparison.Ordinal))
        {
            models = [.. lines[0][ModelsPrefix.Length..].Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries).Select(WithoutLine)];
        }

        string? bounds = null;
        var properties = new Dictionary<string, string>(StringComparer.Ordinal);
        var vars = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in lines.Skip(1))
        {
            if (line.StartsWith("Bounds:", StringComparison.Ordinal))
            {
                bounds = line["Bounds:".Length..].Trim();
            }
            else if (line.StartsWith("Vars:", StringComparison.Ordinal))
            {
                ReadVars(line["Vars:".Length..], vars);
            }
            else if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                ReadProperty(line[2..], properties);
            }
        }

        return new ModelReadme(models, string.IsNullOrWhiteSpace(bounds) ? null : bounds, properties, vars);
    }

    private static void ReadVars(string text, Dictionary<string, string> vars)
    {
        foreach (var entry in text.Split('·', StringSplitOptions.RemoveEmptyEntries))
        {
            if (entry.IndexOf('=', StringComparison.Ordinal) is var at and > 0)
            {
                vars[entry[..at].Trim()] = entry[(at + 1)..].Trim();
            }
        }
    }

    private static void ReadProperty(string text, Dictionary<string, string> properties)
    {
        if (text.IndexOf(':', StringComparison.Ordinal) is var colon and > 0)
        {
            properties[text[..colon].Trim()] = text[(colon + 1)..].Trim();
        }
    }

    // 'src/Timers.cs:57' names src/Timers.cs: line numbers drift with every edit, so matching is on the path only.
    private static string WithoutLine(string token)
    {
        var colon = token.LastIndexOf(':');
        return colon > 0 && int.TryParse(token[(colon + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out _) ? token[..colon] : token;
    }

    /// <summary>Whether a changed path touches what this README says the spec models.</summary>
    /// <param name="folder">The spec folder, workspace-relative.</param>
    /// <param name="changed">A changed path of the repo.</param>
    /// <returns>True when the path is a modelled path, under a modelled folder or one of the spec's own files.</returns>
    internal bool Touches(string folder, string changed)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(changed);
        return changed.StartsWith(folder + "/", StringComparison.Ordinal)
            || (Models ?? []).Any(m => m.EndsWith('/') ? changed.StartsWith(m, StringComparison.Ordinal) : string.Equals(changed, m, StringComparison.Ordinal));
    }
}
