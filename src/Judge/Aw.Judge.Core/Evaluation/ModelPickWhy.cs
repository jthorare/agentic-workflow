namespace Aw.Judge.Core.Evaluation;

/// <summary>Why Jev did not decide a model-pick call, as the rules fallback records it; shared by the call's two providers.</summary>
internal sealed class ModelPickWhy
{
    /// <summary>Jev's input was rejected or cut: a 4xx status, or a timeout. The pick is not retried.</summary>
    public const string InputRejected = "jev-input-rejected";

    /// <summary>Jev answered with a key that is not one of the candidates.</summary>
    public const string UnknownKey = "unknown-key";

    /// <summary>What the rules record when Jev was never asked (not in the chain).</summary>
    public const string RulesOnly = "rules";

    /// <summary>The reason Jev's last answer was not used, or null when it was (or Jev was not asked).</summary>
    public string? Reason { get; set; }
}
