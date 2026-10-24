using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.Decide;

/// <summary>A human decides a decision (POST /sindri/decisions/{decisionId}, 'sindri decide'; API spec §3.2): decision class, appends 'decision.recorded'.</summary>
/// <param name="Decision">The decision.</param>
/// <param name="Choice">One of its options.</param>
/// <param name="Note">A note, or null: at most 65,536 characters, scrubbed.</param>
public sealed record DecideRequest(DecisionId Decision, DecisionOption Choice, string? Note) : ICommand<CommandAccepted>;
