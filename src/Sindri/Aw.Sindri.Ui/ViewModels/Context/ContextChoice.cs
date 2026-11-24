namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>One "Did you mean" choice (#2809): the title a person reads, the node's kind as a small tag, and the key a pick reads. A title another choice
/// shares also reads the key's path, so the rows differ (#3124).</summary>
/// <param name="Key">The node key; <see cref="ContextPageViewModel.Pick(string, string?, CancellationToken)"/> reads it.</param>
/// <param name="Title">The node's label, as the map's cards show it; the key when the server sent no label.</param>
/// <param name="Kind">The node's kind ('feature', 'concept', ...), or empty when unknown.</param>
internal sealed record ContextChoice(string Key, string Title, string Kind)
{
    /// <summary>The key after its kind ("tabs.pane-split") when another choice shares the title; empty otherwise.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>Whether the kind tag shows.</summary>
    public bool HasKind => Kind.Length > 0;

    /// <summary>Whether the path shows.</summary>
    public bool HasPath => Path.Length > 0;

    /// <summary>The muted text after the title: " · tabs.pane-split"; empty with no path.</summary>
    public string PathText => HasPath ? Labels.ContextChoicePath(Path) : string.Empty;

    /// <summary>What a screen reader says: the title, then the kind, then the path.</summary>
    public string Name => string.Join(", ", ((string[])[Title, Kind, Path]).Where(static p => p.Length > 0));

    /// <summary>The choices as the row draws them: each one whose title another shares gets its key's path (#3124); the rest stay as they are.</summary>
    /// <param name="choices">The choices, in order.</param>
    /// <returns>The same choices, in the same order.</returns>
    internal static IReadOnlyList<ContextChoice> WithPaths(IReadOnlyList<ContextChoice> choices)
    {
        var shared = choices.GroupBy(static c => c.Title, StringComparer.Ordinal).Where(static g => g.Skip(1).Any()).Select(static g => g.Key).ToHashSet(StringComparer.Ordinal);
        return [.. choices.Select(c => shared.Contains(c.Title) ? c with { Path = PathOf(c.Key) } : c)];
    }

    // "feature:tabs.pane-split" reads "tabs.pane-split"; a key with no kind reads whole.
    private static string PathOf(string key) => key.IndexOf(':', StringComparison.Ordinal) is var at and >= 0 ? key[(at + 1)..] : key;
}
