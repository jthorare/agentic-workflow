namespace Aw.Sindri.Core.Setup;

/// <summary>The index sources (github, slack) the approved profile names, each with whether its token is connected.</summary>
/// <param name="Count">How many sources the profile names.</param>
public sealed record SourcesFact(int Count)
{
    /// <summary>One line per named source, by its lower-case name: done with "connected" when its token pointer resolves, to do with "needs token" when it doesn't.</summary>
    public SetupItems Items { get; init; } = SetupItems.None;
}
