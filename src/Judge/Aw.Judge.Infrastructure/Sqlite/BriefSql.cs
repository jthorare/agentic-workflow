using Aw.Judge.Core.Briefs;
using Aw.Judge.Core.Ids;
using Aw.Kernel.Data;

namespace Aw.Judge.Infrastructure.Sqlite;

/// <summary>The brief store's SQL: named constants and the statements that bind them.</summary>
internal static class BriefSql
{
    internal const string Insert =
        "INSERT INTO briefs (tool_use_id, agent_id, session_id, prompt_id, dispatch_name, subagent_type, goal, " +
        "acceptance_criteria, proof_command, saved_at) VALUES ($tool_use_id, $agent_id, $session_id, $prompt_id, " +
        "$dispatch_name, $subagent_type, $goal, $acceptance_criteria, $proof_command, $saved_at)";

    private const string Columns =
        "SELECT tool_use_id, agent_id, session_id, prompt_id, dispatch_name, subagent_type, goal, acceptance_criteria, " +
        "proof_command, saved_at FROM briefs ";

    internal const string ByToolUseId = Columns + "WHERE tool_use_id = $tool_use_id";

    // Oldest first; rowid breaks a tie in saved_at by save order.
    internal const string FirstUnmappedByDispatchName =
        Columns + "WHERE dispatch_name = $dispatch_name AND agent_id IS NULL ORDER BY saved_at ASC, rowid ASC LIMIT 1";

    internal const string FirstUnmappedByDispatch =
        Columns + "WHERE session_id = $session_id AND prompt_id = $prompt_id AND subagent_type = $subagent_type " +
        "AND agent_id IS NULL ORDER BY saved_at ASC, rowid ASC LIMIT 1";

    internal const string SetAgentId = "UPDATE briefs SET agent_id = $agent_id WHERE tool_use_id = $tool_use_id";

    internal static SqlStatement Save(Brief b) => new(Insert, c => c
        .Bind("$tool_use_id", b.ToolUseId.Value).Bind("$agent_id", b.AgentId?.Value).Bind("$session_id", b.SessionId.Value)
        .Bind("$prompt_id", b.PromptId.Value).Bind("$dispatch_name", b.DispatchName).Bind("$subagent_type", b.SubagentType)
        .Bind("$goal", b.Goal).Bind("$acceptance_criteria", b.AcceptanceCriteria).Bind("$proof_command", b.ProofCommand)
        .Bind("$saved_at", b.SavedAt), "brief " + b.ToolUseId.Value);

    internal static SqlStatement Find(ToolUseId id) =>
        new(ByToolUseId, c => c.Bind("$tool_use_id", id.Value), "brief " + id.Value);

    internal static SqlStatement FindByName(string name) =>
        new(FirstUnmappedByDispatchName, c => c.Bind("$dispatch_name", name), "briefs named " + name);

    internal static SqlStatement FindByDispatch(SessionId session, PromptId prompt, string subagentType) =>
        new(FirstUnmappedByDispatch, c => c
            .Bind("$session_id", session.Value).Bind("$prompt_id", prompt.Value).Bind("$subagent_type", subagentType));

    internal static SqlStatement Map(ToolUseId id, AgentId agent) =>
        new(SetAgentId, c => c.Bind("$tool_use_id", id.Value).Bind("$agent_id", agent.Value), "brief " + id.Value);
}
