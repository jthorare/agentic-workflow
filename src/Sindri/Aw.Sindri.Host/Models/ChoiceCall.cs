using Aw.Context.Core.Models;

namespace Aw.Sindri.Host.Models;

/// <summary>One Jev pick call in flight: what kind of pick it is, how many questions it carries and the characters they hold, and when it started.</summary>
/// <param name="Kind">The pick.</param>
/// <param name="Questions">The questions of the pack.</param>
/// <param name="StartedAt">When it started.</param>
/// <param name="Stamp">The clock's timestamp at the start, for the elapsed time.</param>
internal sealed record ChoiceCall(ChoiceKind Kind, IReadOnlyList<ChoiceQuestion> Questions, DateTimeOffset StartedAt, long Stamp);
