namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>
/// What a filter box asks for: plain words plus the tokens "p:" provider, "r:" repo, "s:" state, "n:" name and "o:" owner or launcher.
/// A plain class on strings, with no Avalonia and no row type, so the Agents page can hand it to a shared row list unchanged.
/// </summary>
internal sealed class AgentFilterQuery
{
    /// <summary>The query with nothing in it: every row matches.</summary>
    internal static AgentFilterQuery Empty { get; } = new();

    /// <summary>Words that match the name, the repo or what the session is doing.</summary>
    internal IReadOnlyList<string> Words { get; private init; } = [];

    /// <summary>The "p:" values.</summary>
    internal IReadOnlyList<string> Providers { get; private init; } = [];

    /// <summary>The "r:" values.</summary>
    internal IReadOnlyList<string> Repos { get; private init; } = [];

    /// <summary>The "s:" values.</summary>
    internal IReadOnlyList<string> States { get; private init; } = [];

    /// <summary>The "n:" values.</summary>
    internal IReadOnlyList<string> Names { get; private init; } = [];

    /// <summary>The "o:" values.</summary>
    internal IReadOnlyList<string> Owners { get; private init; } = [];

    /// <summary>True when the query narrows nothing.</summary>
    internal bool IsEmpty => Words.Count + Providers.Count + Repos.Count + States.Count + Names.Count + Owners.Count == 0;

    /// <summary>Reads the text of the filter box.</summary>
    /// <param name="text">The box's text; null or blank for none.</param>
    /// <returns>The query.</returns>
    internal static AgentFilterQuery Parse(string? text) => throw new NotSupportedException();

    /// <summary>True when the row passes every word and every token.</summary>
    /// <param name="row">The row's searchable text.</param>
    /// <returns>True when the row stays in the list.</returns>
    internal bool Matches(AgentFilterTarget row) => throw new NotSupportedException();
}
