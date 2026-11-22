namespace Aw.Sindri.Core.Evolve;

/// <summary>Everything one gate run reads.</summary>
/// <param name="Original">The local original.</param>
/// <param name="Rewrite">The generalized rewrite meant to go public.</param>
/// <param name="JudgeFoundLeak">What the separate judge call said about the rewrite.</param>
/// <param name="Sources">The turns the model saw, by session.</param>
internal sealed record GateInput(Proposal Original, Proposal Rewrite, bool JudgeFoundLeak, IReadOnlyList<LeakSource> Sources);
