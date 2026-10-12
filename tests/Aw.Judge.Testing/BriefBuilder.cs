using Aw.Judge.Core.Briefs;
using Aw.Judge.Core.Ids;
using Aw.Kernel.Testing;

namespace Aw.Judge.Testing;

/// <summary>A Brief with deterministic defaults: unmapped, dispatched by session-1/prompt-1 to a general-purpose agent.</summary>
public sealed class BriefBuilder : Builder<BriefBuilder, Brief>
{
    private Brief _brief = new()
    {
        ToolUseId = ToolUseId.From("toolu_1"),
        SessionId = SessionId.From("session-1"),
        PromptId = PromptId.From("prompt-1"),
        DispatchName = null,
        SubagentType = "general-purpose",
        Goal = "Fix the failing test",
        AcceptanceCriteria = "The test passes",
        ProofCommand = "npm test",
        SavedAt = KnownInstants.Epoch,
    };

    /// <summary>Sets the tool use id.</summary>
    /// <param name="toolUseId">The id text.</param>
    /// <returns>This builder.</returns>
    public BriefBuilder WithToolUseId(string toolUseId)
    {
        _brief = _brief with { ToolUseId = ToolUseId.From(toolUseId) };
        return this;
    }

    /// <summary>Sets the teammate name it was dispatched under.</summary>
    /// <param name="name">The name.</param>
    /// <returns>This builder.</returns>
    public BriefBuilder Named(string name)
    {
        _brief = _brief with { DispatchName = name };
        return this;
    }

    /// <summary>Sets the dispatching session, prompt and subagent type.</summary>
    /// <param name="session">The session id text.</param>
    /// <param name="prompt">The prompt id text.</param>
    /// <param name="subagentType">The subagent type.</param>
    /// <returns>This builder.</returns>
    public BriefBuilder DispatchedBy(string session, string prompt, string subagentType)
    {
        _brief = _brief with { SessionId = SessionId.From(session), PromptId = PromptId.From(prompt), SubagentType = subagentType };
        return this;
    }

    /// <summary>Sets when it was saved.</summary>
    /// <param name="savedAt">The time.</param>
    /// <returns>This builder.</returns>
    public BriefBuilder SavedAt(DateTimeOffset savedAt)
    {
        _brief = _brief with { SavedAt = savedAt };
        return this;
    }

    /// <inheritdoc />
    public override Brief Build() => _brief;
}
