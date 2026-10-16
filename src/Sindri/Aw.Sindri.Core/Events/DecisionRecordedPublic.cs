using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The public form of 'decision.recorded' (API spec §3.4): the payload without its note, so a session of the same repo sees the decision and the choice but not what the human wrote.</summary>
/// <param name="CommandId">The 'Decide' command's id.</param>
/// <param name="Decision">The decision.</param>
/// <param name="Choice">The option chosen.</param>
/// <param name="By">Who decided, as the envelope's actor reads.</param>
/// <param name="Repo">The decision's repo, or null.</param>
public sealed record DecisionRecordedPublic(CommandId CommandId, DecisionId Decision, DecisionOption Choice, string By, RepoName? Repo);
