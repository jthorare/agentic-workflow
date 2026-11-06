using Aw.Kernel;

namespace Aw.Sindri.Core.Skills;

/// <summary>The reader's decision for one turn end.</summary>
/// <param name="Kind">What happens.</param>
/// <param name="Reason">Why the owner is waited on; null unless <see cref="SkillResultKind.OwnerWaiting"/>.</param>
/// <param name="Outcome">The outcome name on <see cref="SkillResultKind.Complete"/>.</param>
/// <param name="Facts">The fact key to value on <see cref="SkillResultKind.Complete"/>.</param>
/// <param name="Text">The question on a 'waiting' result, redacted by the caller before it is recorded.</param>
/// <param name="Error">The SND-SKILL error, or null.</param>
/// <param name="Detail">The parse error, the other skill's name, or the step.failed sentence.</param>
public sealed record SkillResultDecision(
    SkillResultKind Kind,
    SkillWaitReason? Reason,
    string? Outcome,
    IReadOnlyDictionary<string, string> Facts,
    string? Text,
    ErrorDefinition? Error,
    string? Detail);
