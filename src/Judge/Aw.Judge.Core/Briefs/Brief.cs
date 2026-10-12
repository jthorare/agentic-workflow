using Aw.Judge.Core.Ids;

namespace Aw.Judge.Core.Briefs;

/// <summary>A subagent dispatch brief (the 'briefs' table): saved by the scope gate, mapped to the agent when it starts.</summary>
public sealed record Brief
{
    /// <summary>The tool call that dispatched the subagent.</summary>
    public required ToolUseId ToolUseId { get; init; }

    /// <summary>The subagent's id once it started and was mapped; null until then.</summary>
    public AgentId? AgentId { get; init; }

    /// <summary>The session that dispatched it.</summary>
    public required SessionId SessionId { get; init; }

    /// <summary>The prompt that dispatched it.</summary>
    public required PromptId PromptId { get; init; }

    /// <summary>The teammate name it was dispatched under, when it had one.</summary>
    public string? DispatchName { get; init; }

    /// <summary>The subagent type.</summary>
    public required string SubagentType { get; init; }

    /// <summary>What the subagent is for.</summary>
    public required string Goal { get; init; }

    /// <summary>How its result is judged done.</summary>
    public required string AcceptanceCriteria { get; init; }

    /// <summary>The command that proves it.</summary>
    public required string ProofCommand { get; init; }

    /// <summary>When the brief was saved.</summary>
    public required DateTimeOffset SavedAt { get; init; }
}
