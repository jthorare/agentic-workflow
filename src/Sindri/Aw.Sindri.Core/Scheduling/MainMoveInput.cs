namespace Aw.Sindri.Core.Scheduling;

/// <summary>What <see cref="MainMoveDecision"/> compares: the checkouts' heads against the last success's, and whether the rules the last success was built under changed.</summary>
/// <param name="OnDefaultHeads">The HEAD of each repo whose checkout is on its default branch, by repo name.</param>
/// <param name="LastSucceededHeads">The heads the last successful job read, or null when none has succeeded yet.</param>
/// <param name="RulesChanged">Whether the running build's rules differ from the ones the last success was built under.</param>
public sealed record MainMoveInput(IReadOnlyDictionary<string, string> OnDefaultHeads, IReadOnlyDictionary<string, string>? LastSucceededHeads, bool RulesChanged);
