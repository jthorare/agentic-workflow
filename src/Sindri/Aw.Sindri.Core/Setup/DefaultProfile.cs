using System.Globalization;
using System.Text;

namespace Aw.Sindri.Core.Setup;

/// <summary>
/// The profile the first AddRepo creates (setup-once spec §5.1): the schema version, the user, and 'workflow.effects' set to the effect kinds the shipped recipes declare, so the first work item's
/// effects are not refused (SND-WF-011) once the recipes are approved, and 'workflow.scoping.historyWindow' empty (the open window), the key the scoping recipe names. Everything else takes its default. The repos are appended after it.
/// </summary>
public static class DefaultProfile
{
    /// <summary>The text of a new profile file.</summary>
    /// <param name="user">Who runs this Sindri.</param>
    /// <param name="effects">The effect kinds ('effect.claim.ref' and so on) the shipped recipes declare.</param>
    /// <returns>The YAML, ending in a newline, with no 'repos' key yet.</returns>
    public static string Text(string user, IReadOnlyCollection<string> effects)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(effects);
        var text = new StringBuilder();
        _ = text.Append(CultureInfo.InvariantCulture, $"schemaVersion: 1\nuser: {Quote(user)}\n");
        if (effects.Count == 0)
        {
            _ = text.Append("workflow:\n  effects: []\n");
        }
        else
        {
            _ = text.Append("workflow:\n  effects:\n");
            foreach (var effect in effects.Order(StringComparer.Ordinal))
            {
                _ = text.Append(CultureInfo.InvariantCulture, $"    - {effect}\n");
            }
        }

        // the scoping recipe names this key (#399): an empty window is all history, the behavior before the key existed
        return text.Append("  scoping:\n    historyWindow: {}\n").ToString();
    }

    /// <summary>A YAML double-quoted scalar for <paramref name="value"/>.</summary>
    /// <param name="value">The text.</param>
    /// <returns>The quoted text.</returns>
    public static string Quote(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal) + "\"";
    }
}
