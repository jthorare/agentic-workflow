using Aw.Judge.Core.Ids;

namespace Aw.Judge.Core.Labels;

/// <summary>An 'ask-check' item made from a decision that has no outcome label yet: what 'label outcomes' works on.</summary>
/// <param name="Item">The item.</param>
/// <param name="DecidedAt">When the decision was made.</param>
/// <param name="Decision">'continue' or 'ask'.</param>
/// <param name="Session">The session the decision came from, when its details kept one.</param>
public sealed record OutcomeCandidate(EvalItemId Item, DateTimeOffset DecidedAt, string Decision, SessionId? Session);
