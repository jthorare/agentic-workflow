using Aw.Sindri.Core.Line;

namespace Aw.Sindri.Features.Plan;

/// <summary>Run the plan packet's checks over the files a caller holds (line spec §6.3; 'sindri plan check').</summary>
/// <param name="Facts">Text by name: the plan packet's files ('tasks.json', 'graph.json' …), the design packet's under 'design/', aw-packets' answer as 'validation.json', and optionally 'options.json'; a missing name makes the rules that need it not-run.</param>
/// <param name="Stage">The stage to run.</param>
public sealed record PlanCheckRequest(IReadOnlyDictionary<string, string> Facts, LineStage Stage);
