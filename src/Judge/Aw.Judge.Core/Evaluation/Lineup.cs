using Aw.Judge.Core.Ports;

namespace Aw.Judge.Core.Evaluation;

/// <summary>What one walk of the chain runs with: the providers to try in order and the call's images.</summary>
internal sealed record Lineup(IReadOnlyList<IModelProvider> Candidates, CallImages Images);
