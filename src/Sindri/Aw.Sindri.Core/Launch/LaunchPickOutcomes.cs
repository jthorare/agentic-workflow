namespace Aw.Sindri.Core.Launch;

/// <summary>The outcome words of a run in 'sindri launch picks' and GET /sindri/launch/picks (model-pick spec §6.1).</summary>
public static class LaunchPickOutcomes
{
    /// <summary>The run's report was refused.</summary>
    public const string Refused = "refused";

    /// <summary>The run failed its check, crashed or never started.</summary>
    public const string Failed = "failed";

    /// <summary>The run's report was accepted.</summary>
    public const string Passed = "passed";

    /// <summary>The run has not ended.</summary>
    public const string Running = "running";
}
