using Aw.Judge.Core.Decisions;
using Aw.Judge.Core.Ids;
using Aw.Kernel.Testing;

namespace Aw.Judge.Testing;

/// <summary>A DecisionRecord with deterministic defaults: a decided wake-gate answer with a detail row.</summary>
public sealed class DecisionBuilder : Builder<DecisionBuilder, DecisionRecord>
{
    private DecisionRecord _record = new()
    {
        Id = DecisionId.From("00000000-0000-4000-8000-000000000001"),
        Ts = KnownInstants.Epoch,
        Question = "wake-gate",
        ContentClass = "general",
        Provider = "claude-cli",
        Decision = "wake",
        Confidence = 0.875,
        ReasonCode = "model",
        LatencyMs = 1234,
        InputDigest = "0123456789abcdef",
        ChainPosition = 1,
        Skipped = [new SkippedProvider("jev", SkipReason.Unavailable)],
        Outcome = DecisionOutcome.Decided,
        Details = new DecisionDetails
        {
            InputJson = """{"message":"hello"}""",
            Probabilities = new Dictionary<string, double>(StringComparer.Ordinal) { ["wake"] = 0.875, ["sleep"] = 0.125 },
            RulesOpinion = "sleep",
            Agreement = Agreement.Overrode,
            SessionId = SessionId.From("session-1"),
        },
    };

    /// <summary>Sets the id.</summary>
    /// <param name="id">The id text.</param>
    /// <returns>This builder.</returns>
    public DecisionBuilder WithId(string id)
    {
        _record = _record with { Id = DecisionId.From(id) };
        return this;
    }

    /// <summary>Sets when it was decided.</summary>
    /// <param name="ts">The time.</param>
    /// <returns>This builder.</returns>
    public DecisionBuilder At(DateTimeOffset ts)
    {
        _record = _record with { Ts = ts };
        return this;
    }

    /// <summary>Sets the outcome; an escalated or failed one has no decision.</summary>
    /// <param name="outcome">The outcome.</param>
    /// <returns>This builder.</returns>
    public DecisionBuilder WithOutcome(DecisionOutcome outcome)
    {
        _record = _record with { Outcome = outcome, Decision = outcome == DecisionOutcome.Decided ? _record.Decision : null };
        return this;
    }

    /// <summary>Makes it a pre-rule settlement: decided by 'rules' without reaching a provider.</summary>
    /// <returns>This builder.</returns>
    public DecisionBuilder SettledByPreRule()
    {
        _record = _record with { Outcome = DecisionOutcome.Decided, Provider = "rules", ReasonCode = "pre-rule", Confidence = 1, ChainPosition = 0, Skipped = [] };
        return this;
    }

    /// <summary>Makes it a rule-timeout escalation: a question rule timed out before the chain, so no provider was asked.</summary>
    /// <returns>This builder.</returns>
    public DecisionBuilder RuleTimedOut()
    {
        _record = _record with { Outcome = DecisionOutcome.Failed, Decision = null, Provider = "none", ReasonCode = "rule-timeout", Confidence = 0, ChainPosition = 0, Skipped = [] };
        return this;
    }

    /// <summary>Drops the detail row.</summary>
    /// <returns>This builder.</returns>
    public DecisionBuilder WithoutDetails()
    {
        _record = _record with { Details = null };
        return this;
    }

    /// <summary>Keeps the detail row but with only the input (every optional field null).</summary>
    /// <returns>This builder.</returns>
    public DecisionBuilder WithBareDetails()
    {
        _record = _record with { Details = new DecisionDetails { InputJson = "{}" } };
        return this;
    }

    /// <summary>Clears the decision, the skipped list and the undo time, as an escalated row with nothing tried.</summary>
    /// <returns>This builder.</returns>
    public DecisionBuilder WithNothingTried()
    {
        _record = _record with { Decision = null, Skipped = [], Provider = "none", ChainPosition = 0 };
        return this;
    }

    /// <inheritdoc />
    public override DecisionRecord Build() => _record;
}
