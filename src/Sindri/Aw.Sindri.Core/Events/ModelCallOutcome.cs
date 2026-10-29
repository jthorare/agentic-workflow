namespace Aw.Sindri.Core.Events;

/// <summary>The values of <see cref="ModelCallTry.Outcome"/>.</summary>
public static class ModelCallOutcome
{
    /// <summary>The harness answered.</summary>
    public const string Answered = "answered";

    /// <summary>The harness is at its usage limit.</summary>
    public const string Limit = "limit";

    /// <summary>The harness failed for any other reason.</summary>
    public const string Failed = "failed";

    /// <summary>Not tried: limited earlier in this job, or the prompt was too long for it.</summary>
    public const string Skipped = "skipped";

    /// <summary>A Jev pick step that was skipped (reason 'no-key' or 'off').</summary>
    public const string NotAsked = "not-asked";
}
