namespace Aw.Context.Core.Results;

/// <summary>One section of a result.</summary>
/// <param name="Name">'graph', 'affects', 'decisions', 'code' or 'records'.</param>
/// <param name="Items">Every ranked item of the section, best first.</param>
public sealed record ResultSection(string Name, IReadOnlyList<ResultItem> Items)
{
    /// <summary>The section names, in fill order (spec §8.3).</summary>
    public static IReadOnlyList<string> FillOrder { get; } = ["graph", "affects", "decisions", "code", "records"];
}
