using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Core.Evaluation;

/// <summary>What one evaluation runs with: the providers built for this process, the chain, and the question's config.</summary>
public sealed record EvaluationContext
{
    /// <summary>Every provider available to this process (the chain picks and orders them).</summary>
    public required IReadOnlyList<IModelProvider> Providers { get; init; }

    /// <summary>The routing; the default chain when not set.</summary>
    public ChainSpec Chain { get; init; } = ChainSpec.Default;

    /// <summary>The question's config entry, or null when config.json has none (enabled, the question's threshold).</summary>
    public QuestionOverride? Override { get; init; }

    /// <summary>The cap on choose's prompt facts, in characters (config.json's providers.inputCapChars); null for the default.</summary>
    public int? InputCapChars { get; init; }

    /// <summary>The calling session, stored with the decision's details.</summary>
    public SessionId? SessionId { get; init; }
}
