using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Scoping;

/// <summary>What 'step.scope.challenge' is told about its output (#1102): one round, in the shape of schemas/scoping/challenge-round.json, never a copy of the challenge log it reads.</summary>
internal sealed class ScopeChallengeInstructions : IAgentStepInstructions
{
    private static readonly BlockId _step = BlockId.From("step.scope.challenge");

    private static readonly string[] _lines =
    [
        "Your output fact:challengeRound is this round only, one JSON object as schemas/scoping/challenge-round.json: walked (the lenses and dimensions you walked), verdict (nothingNew or found), and for found the new surfaces (with citations) and new behaviours (with the question each raises).",
        "challenge.json is the log of the earlier rounds ({\"rounds\":[...]}), not your output: never copy it, and never put a \"rounds\" array in fact:challengeRound. Sindri appends your round to the log.",
    ];

    /// <inheritdoc/>
    public bool IsFor(BlockId step) => step == _step;

    /// <inheritdoc/>
    public IReadOnlyList<string> Lines => _lines;
}
