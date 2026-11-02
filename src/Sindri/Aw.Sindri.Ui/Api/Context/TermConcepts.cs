namespace Aw.Sindri.Ui.Api.Context;

/// <summary>One term and the concepts it characterizes.</summary>
/// <param name="Term">The term.</param>
/// <param name="Concepts">The ids of the concepts.</param>
internal sealed record TermConcepts(string Term, IReadOnlyList<string> Concepts);
