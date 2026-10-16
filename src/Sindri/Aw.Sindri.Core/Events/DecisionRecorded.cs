using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'decision.recorded' payload (API spec §3.4): a human chose an option. Its public form, <see cref="DecisionRecordedPublic"/>, leaves the note out; a human's ledger read keeps it whole.</summary>
/// <param name="CommandId">The 'Decide' command's id.</param>
/// <param name="Decision">The decision.</param>
/// <param name="Choice">The option chosen.</param>
/// <param name="Note">The note given with it (the projection scrubs it), or null.</param>
/// <param name="By">Who decided, as the envelope's actor reads.</param>
/// <param name="Repo">The decision's repo, copied from its row, or null; a session sees an item-less decision by it.</param>
public sealed record DecisionRecorded(CommandId CommandId, DecisionId Decision, DecisionOption Choice, string? Note, string By, RepoName? Repo = null);
