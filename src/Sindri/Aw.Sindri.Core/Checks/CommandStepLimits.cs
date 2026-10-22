namespace Aw.Sindri.Core.Checks;

/// <summary>How long the Steps that run the repo's commands may run (line spec LN-WF7): the descriptors take their MaxDuration from here and the Steps their command budget, so the two cannot drift apart.</summary>
public static class CommandStepLimits
{
    /// <summary>Gets the longest 'step.build.red-check' and 'step.build.checks' may run: the default 'workflow.steps.longMax', the cap on what a Step declares. A descriptor is static, so a longMax above it does not lengthen the Steps.</summary>
    public static TimeSpan BuildMaxDuration { get; } = TimeSpan.FromHours(2);

    /// <summary>Gets the longest 'step.ship.green' may run: the same as the build Steps.</summary>
    public static TimeSpan GreenMaxDuration { get; } = BuildMaxDuration;

    /// <summary>Gets the longest 'step.prove.before-after' may run: it runs each recorded script at the base and at the head (20 minutes each at most), so an hour and a half holds two signals end to end and stays inside the 2 h state timeout.</summary>
    public static TimeSpan ProveMaxDuration { get; } = TimeSpan.FromMinutes(90);

    /// <summary>Gets the time the slot wait and the commands of a build Step share.</summary>
    public static TimeSpan BuildBudget { get; } = RepoCommandRunner.BudgetFor(BuildMaxDuration);

    /// <summary>Gets the time the slot wait and the commands of 'step.ship.green' share.</summary>
    public static TimeSpan GreenBudget { get; } = RepoCommandRunner.BudgetFor(GreenMaxDuration);
}
