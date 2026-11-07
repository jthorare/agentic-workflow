using System.Globalization;
using System.Text;
using Aw.Sindri.Features.GetSkills;

namespace Aw.Sindri.Host.Commands;

/// <summary>'sindri skills' as text (skill flows spec §7): one line per loaded block with the path it came from, then a 'not loaded' line per block that was skipped.</summary>
internal static class SkillsText
{
    /// <summary>The list.</summary>
    /// <param name="skills">The answer.</param>
    /// <returns>The text.</returns>
    internal static string Render(GetSkillsResponse skills)
    {
        ArgumentNullException.ThrowIfNull(skills);
        var text = new StringBuilder();
        foreach (var block in skills.Blocks)
        {
            _ = text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{block.Id}  v{block.Version}  {string.Join('/', block.Outcomes)}{(block.Pushes ? "  pushes" : string.Empty)}  {block.Path}"));
        }

        foreach (var skipped in skills.NotLoaded ?? [])
        {
            _ = text.AppendLine($"not loaded: {skipped.Id}: {skipped.Reason}");
        }

        return text.Length == 0 ? "no skill blocks\n" : text.ToString();
    }
}
