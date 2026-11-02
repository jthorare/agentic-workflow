namespace Aw.Judge.Core.Evaluation;

/// <summary>The reason codes the engine itself writes (providers bring their own).</summary>
public static class EvaluationReasons
{
    /// <summary>The question is disabled in config.</summary>
    public const string QuestionDisabled = "question-disabled";

    /// <summary>The input failed the question's schema.</summary>
    public const string InvalidInput = "invalid-input";

    /// <summary>A pre-rule settled it.</summary>
    public const string PreRule = "pre-rule";

    /// <summary>A fallback rule settled it after a below-threshold answer.</summary>
    public const string FallbackAfterUndecided = "fallback-after-undecided";

    /// <summary>The last provider to answer was below threshold and nothing settled it.</summary>
    public const string BelowThreshold = "below-threshold";

    /// <summary>No provider decided.</summary>
    public const string NoProviderDecided = "no-provider-decided";

    /// <summary>A question rule (input check, pre-rule, prompt or fallback) hit its regex match timeout; failure row and escalation.</summary>
    public const string RuleTimeout = "rule-timeout";

    /// <summary>
    /// The reason codes of the decision rows written before the chain is walked, so no model was asked: health's
    /// 'down' streak counts model attempts only and leaves these out. A fallback that times out writes only a failure
    /// row, so every 'rule-timeout' decision row is one of these.
    /// </summary>
    public static IReadOnlyList<string> BeforeChain { get; } = [QuestionDisabled, InvalidInput, PreRule, RuleTimeout];

    /// <summary>Failure row: the provider answered outside the question's outputs.</summary>
    public const string OutOfEnum = "out-of-enum";

    /// <summary>Failure row: the provider decided an output that needs reasons without giving any.</summary>
    public const string NoReasons = "no-reasons";

    /// <summary>Failure row: the decision row couldn't be written (the outcome still stands).</summary>
    public const string DecisionWriteFailed = "decision-write-failed";

    /// <summary>The provider column of an escalation row.</summary>
    public const string NoProvider = "none";
}
