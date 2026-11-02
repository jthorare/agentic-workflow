using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Judge;

/// <summary>The judge's model-pick question as a request (model-pick spec §4.2).</summary>
/// <param name="Step">The step's block id and state id ('step.interview.questions questions').</param>
/// <param name="Kind">'draft', 'challenge', 'questions', 'apply' or 'other'.</param>
/// <param name="Brief">The rendered step brief.</param>
/// <param name="Files">The pack's files and the written facts.</param>
/// <param name="Candidates">One to 32 models after the constraints, cheapest first.</param>
/// <param name="DenyPaths">The profile's index.denyPaths globs.</param>
/// <param name="History">Per candidate, how the model fared on this step.</param>
public sealed record ModelPickAsk(string Step, string Kind, string Brief, IReadOnlyList<PickFileText> Files, IReadOnlyList<PickCandidate> Candidates, IReadOnlyList<string> DenyPaths, IReadOnlyList<ModelPickHistoryRow> History);
