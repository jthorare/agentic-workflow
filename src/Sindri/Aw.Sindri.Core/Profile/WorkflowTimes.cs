using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Profile;

/// <summary>The workflow engine's times from the profile (S1-D11), each key's default applied.</summary>
/// <param name="DeskTimeout">'workflow.deskTimeout': the floor of every desk Step's timeout.</param>
/// <param name="EventHold">'workflow.eventHold': how long a buffered event waits for its wait.</param>
public sealed record WorkflowTimes(TimeSpan DeskTimeout, TimeSpan EventHold)
{
    /// <summary>The defaults: deskTimeout 2d (W20), eventHold 24h (§5.4).</summary>
    public static WorkflowTimes Defaults { get; } = new(TimeSpan.FromDays(2), TimeSpan.FromHours(24));

    /// <summary>The times <paramref name="profile"/> sets, a key it leaves out (or a profile with no 'workflow:' block) taking its default.</summary>
    /// <param name="profile">A checked profile (<see cref="ProfileRules.Check(SindriProfile)"/> refused any value that isn't a duration).</param>
    /// <returns>The times.</returns>
    public static WorkflowTimes Of(SindriProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return new(
            Span(profile.Workflow?.DeskTimeout) ?? Defaults.DeskTimeout,
            Span(profile.Workflow?.EventHold) ?? Defaults.EventHold);
    }

    private static TimeSpan? Span(string? text) =>
        text is not null && RecipeDuration.TryFrom(text, out var duration) ? duration.Span : null;
}
