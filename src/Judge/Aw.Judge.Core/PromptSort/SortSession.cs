namespace Aw.Judge.Core.PromptSort;

/// <summary>A session's sorter state ('judge/sessions/&lt;sid&gt;.sort.json', a hook-contract file: its shape is frozen, spec §10).</summary>
/// <param name="Prompts">Prompts sorted so far in the session.</param>
/// <param name="LastFired">The prompt number at which each scaffold last fired, by scaffold id.</param>
/// <param name="Requirements">What the session's work needs before it is done.</param>
public sealed record SortSession(int Prompts, IReadOnlyDictionary<string, int> LastFired, SortRequirements Requirements)
{
    /// <summary>A session the sorter hasn't seen.</summary>
    public static SortSession Empty { get; } = new(0, new Dictionary<string, int>(StringComparer.Ordinal), new SortRequirements());
}
