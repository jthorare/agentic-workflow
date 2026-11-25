using System.Globalization;
using System.Text;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Features.StartSession;

/// <summary>
/// The name of a one-off worktree and branch (agents spec §15.4): 'oneoff-yyMMdd-HHmm-slug'. The slug is the prompt's first 3 words in a-z, 0-9 and '-', 24 characters at most, or the agent id for
/// an empty prompt or one that strips to nothing. The pattern is '^oneoff-\d{6}-\d{4}-[a-z0-9-]{1,24}(-\d+)?$'; the maker adds '-2', '-3' for a name that exists.
/// </summary>
internal static class OneOffName
{
    private const int SlugLength = 24;

    internal static string Of(DateTimeOffset local, string? prompt, AgentId agent) =>
        $"oneoff-{local.ToString("yyMMdd-HHmm", CultureInfo.InvariantCulture)}-{Slug(prompt, agent)}";

    internal static string Slug(string? prompt, AgentId agent)
    {
        var words = (prompt ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Take(3);
        var slug = new StringBuilder();
        foreach (var c in string.Join(' ', words).ToLowerInvariant())
        {
            var keep = c is (>= 'a' and <= 'z') or (>= '0' and <= '9');
            if (keep)
            {
                _ = slug.Append(c);
            }
            else if (slug.Length > 0 && slug[^1] != '-')
            {
                _ = slug.Append('-');
            }
        }

        var text = slug.Length > SlugLength ? slug.ToString(0, SlugLength) : slug.ToString();
        text = text.Trim('-');
        return text.Length > 0 ? text : agent.Value;
    }
}
