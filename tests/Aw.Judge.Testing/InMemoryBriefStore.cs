using Aw.Judge.Core.Briefs;
using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Ports;
using Aw.Kernel;

namespace Aw.Judge.Testing;

/// <summary>The in-memory IBriefStore fake; it passes BriefStoreContract.</summary>
public sealed class InMemoryBriefStore : IBriefStore
{
    private static readonly Error _unavailable = new("AW-DATA-002", ErrorKind.Unavailable, "the in-memory brief store is set unavailable");
    private readonly List<Brief> _briefs = [];

    /// <summary>When true every call returns an error, as a store that can't be opened.</summary>
    public bool Unavailable { get; init; }

    /// <summary>The briefs saved so far, in save order.</summary>
    public IReadOnlyList<Brief> Briefs => _briefs;

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Save(Brief brief, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(brief);
        if (Unavailable)
        {
            return Done<Unit>(_unavailable);
        }

        if (IndexOf(brief.ToolUseId) >= 0)
        {
            return Done<Unit>(JudgeErrors.BriefExists.With($"brief already saved: {brief.ToolUseId.Value}"));
        }

        _briefs.Add(brief);
        return Done(Results.Ok());
    }

    /// <inheritdoc />
    public ValueTask<Result<Brief?>> ByToolUseId(ToolUseId toolUseId, CancellationToken cancellationToken) =>
        First(b => b.ToolUseId.Value == toolUseId.Value);

    /// <inheritdoc />
    public ValueTask<Result<Brief?>> FirstUnmappedByDispatchName(string dispatchName, CancellationToken cancellationToken) =>
        First(b => b.AgentId is null && string.Equals(b.DispatchName, dispatchName, StringComparison.Ordinal));

    /// <inheritdoc />
    public ValueTask<Result<Brief?>> FirstUnmappedByDispatch(SessionId sessionId, PromptId promptId, string subagentType, CancellationToken cancellationToken) =>
        First(b => b.AgentId is null && b.SessionId.Value == sessionId.Value && b.PromptId.Value == promptId.Value
            && string.Equals(b.SubagentType, subagentType, StringComparison.Ordinal));

    /// <inheritdoc />
    public ValueTask<Result<Unit>> SetAgentId(ToolUseId toolUseId, AgentId agentId, CancellationToken cancellationToken)
    {
        if (Unavailable)
        {
            return Done<Unit>(_unavailable);
        }

        var index = IndexOf(toolUseId);
        if (index >= 0)
        {
            _briefs[index] = _briefs[index] with { AgentId = agentId };
        }

        return Done(Results.Ok());
    }

    private static ValueTask<Result<T>> Done<T>(Result<T> result) => ValueTask.FromResult(result);

    private static ValueTask<Result<T>> Done<T>(Error error) => ValueTask.FromResult(Results.Failure<T>(error));

    private int IndexOf(ToolUseId toolUseId) => _briefs.FindIndex(b => b.ToolUseId.Value == toolUseId.Value);

    // Oldest first; a tie in SavedAt keeps save order (OrderBy is stable), as the SQL's rowid tie-break does.
    private ValueTask<Result<Brief?>> First(Func<Brief, bool> match) =>
        Unavailable
            ? Done<Brief?>(_unavailable)
            : Done(Results.Success(_briefs.Where(match).OrderBy(b => b.SavedAt).FirstOrDefault()));
}
