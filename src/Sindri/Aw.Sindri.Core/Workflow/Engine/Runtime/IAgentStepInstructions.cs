using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>What an agent Step is told to do (PR babysitter spec Appendix A): the lines the launch adds to the generic brief, after its first line. Registered by the slice that owns the Step.</summary>
public interface IAgentStepInstructions
{
    /// <summary>Whether these lines are for <paramref name="step"/>.</summary>
    /// <param name="step">The agent Step being launched.</param>
    /// <returns>True when the lines belong to that Step.</returns>
    public bool IsFor(BlockId step);

    /// <summary>The lines, in order; plain text, no fact content (the agent reads facts itself, fenced).</summary>
    public IReadOnlyList<string> Lines { get; }
}
