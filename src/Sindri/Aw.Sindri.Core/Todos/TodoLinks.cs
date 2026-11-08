using Aw.Kernel;

namespace Aw.Sindri.Core.Todos;

/// <summary>A todo's links (todo spec §4.3): items, PRs and Linear issues, each stored as the deep link the app opens. One parser per kind, one list of kinds, so a new kind is a row here.</summary>
public static class TodoLinks
{
    /// <summary>The most links a todo takes.</summary>
    public const int Max = 10;

    /// <summary>Reads one typed link: 'ITM-…' (to 'sindri://item/…'), '&lt;owner&gt;/&lt;repo&gt;#&lt;n&gt;' or an https PR url on a configured forge host (to 'sindri://reviews/&lt;owner&gt;/&lt;repo&gt;/&lt;n&gt;'), or a Linear key 'ENG-12' (to 'sindri://linear/ENG-12'). Checks the shape only.</summary>
    /// <param name="typed">The typed link.</param>
    /// <param name="forgeHosts">The forge hosts a PR url may name.</param>
    /// <returns>The deep link, or SND-TODO-004.</returns>
    public static Result<string> Parse(string typed, IReadOnlyCollection<string> forgeHosts)
    {
        ArgumentNullException.ThrowIfNull(typed);
        ArgumentNullException.ThrowIfNull(forgeHosts);
        throw new NotSupportedException("TodoLinks.Parse: make TodoLinksTests pass");
    }

    /// <summary>Reads a whole list: each link parsed, duplicates dropped, at most <see cref="Max"/>.</summary>
    /// <param name="typed">The typed links.</param>
    /// <param name="forgeHosts">The forge hosts a PR url may name.</param>
    /// <returns>The deep links in order, or SND-TODO-004.</returns>
    public static Result<IReadOnlyList<string>> ParseAll(IReadOnlyList<string> typed, IReadOnlyCollection<string> forgeHosts)
    {
        ArgumentNullException.ThrowIfNull(typed);
        ArgumentNullException.ThrowIfNull(forgeHosts);
        throw new NotSupportedException("TodoLinks.ParseAll: make TodoLinksTests pass");
    }
}
