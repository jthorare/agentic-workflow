using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>The checks that need nothing but Core: a payload is a JSON object or array, and 'fact:challengeRound' is one round. The host adds the scoping fact schemas (<c lang="text">ReportOutputChecker</c>).</summary>
public sealed class PayloadReportChecks : IReportOutputChecks
{
    /// <summary>The one instance.</summary>
    public static PayloadReportChecks Instance { get; } = new();

    /// <inheritdoc/>
    public IReadOnlyList<string> Violations(IReadOnlyDictionary<FactKey, ReadOnlyMemory<byte>> outputs, IReadOnlySet<FactKey> payloads, bool scopingQuestions)
    {
        ArgumentNullException.ThrowIfNull(outputs);
        ArgumentNullException.ThrowIfNull(payloads);
        var wrong = outputs.Where(o => payloads.Contains(o.Key)).OrderBy(static o => o.Key.Value, StringComparer.Ordinal)
            .Select(o => PayloadFacts.Violation(o.Key, o.Value.Span)).OfType<string>().ToList();
        wrong.AddRange(outputs.Where(static o => ChallengeRoundShape.Covers(o.Key)).Select(static o => ChallengeRoundShape.Violation(o.Value.Span)).OfType<string>());   // #1102: one round, not the challenge log
        return wrong;
    }
}
