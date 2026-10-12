namespace Aw.Judge.Core.Questions;

/// <summary>brief-scope's input (hook contract: scope-gate.sh sends these fields).</summary>
/// <param name="AgentType">The dispatched agent type.</param>
/// <param name="Goal">The brief's goal.</param>
/// <param name="AcceptanceCriteria">The brief's acceptance criteria.</param>
/// <param name="ProofCommand">The command that proves the work.</param>
/// <param name="SkillInternal">True when a skill dispatches one of its own steps (exempt, like a read-only agent type).</param>
public sealed record BriefScopeInput(string AgentType, string Goal, string AcceptanceCriteria, string ProofCommand, bool? SkillInternal = null);
