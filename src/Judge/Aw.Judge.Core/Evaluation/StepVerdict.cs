namespace Aw.Judge.Core.Evaluation;

/// <summary>What one provider's answer means for the chain (the pure part of each step).</summary>
internal enum StepVerdict
{
    /// <summary>Not available (no key, not installed): skipped, no failure row.</summary>
    Unavailable = 1,

    /// <summary>Ran past the budget: skipped as 'timeout', with a failure row.</summary>
    TimedOut = 2,

    /// <summary>Returned an error: skipped as 'failed', with a failure row carrying its reason.</summary>
    Errored = 3,

    /// <summary>Answered outside the outputs: skipped as 'failed', failure row 'out-of-enum'.</summary>
    OutOfEnum = 4,

    /// <summary>Answered below threshold: skipped as 'below_threshold', no failure row; a fallback rule may settle.</summary>
    BelowThreshold = 5,

    /// <summary>Answered in the outputs and above threshold, but an output that needs reasons came without any: skipped as 'failed', failure row 'no-reasons'.</summary>
    NoReasons = 7,

    /// <summary>Decided.</summary>
    Decided = 6,
}
