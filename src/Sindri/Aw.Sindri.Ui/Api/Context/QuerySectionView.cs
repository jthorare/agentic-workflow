namespace Aw.Sindri.Ui.Api.Context;

/// <summary>One section of a question's answer (graph, affects, code, records).</summary>
/// <param name="Name">The section name.</param>
/// <param name="Items">The items, in the order served.</param>
internal sealed record QuerySectionView(string Name, IReadOnlyList<QueryItemView>? Items);
