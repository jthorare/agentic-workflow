namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>One question for Jev.</summary>
/// <param name="Name">The output field the answer lands in.</param>
/// <param name="Type">'choice' (pick a key of <paramref name="Criteria"/>), 'score' (pick a level; the criteria's values are the levels, lowest first) or 'noul' (a yes/no as a number).</param>
/// <param name="Instructions">The prompt.</param>
/// <param name="Criteria">What each option or level means; null for 'noul'.</param>
public sealed record JevQuestion(string Name, string Type, string Instructions, IReadOnlyDictionary<string, string>? Criteria);
