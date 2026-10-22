using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>What an agent Step is told to do (PR babysitter spec Appendix A): the lines the launch adds to the generic brief, after its first line. Registered by the slice that owns the Step.</summary>
public interface IAgentStepInstructions
{
    /// <summary>The Step these lines are for.</summary>
    public BlockId Step { get; }

    /// <summary>The lines, in order; plain text, no fact content (the agent reads facts itself, fenced).</summary>
    public IReadOnlyList<string> Lines { get; }
}
