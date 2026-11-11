namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>Compiles a 'code' node's body for the validator, so a typo fails at save time (agent-first PR 9c). It runs nothing.</summary>
public interface ICodeCheck
{
    /// <summary>Compiles the body.</summary>
    /// <param name="body">The method body.</param>
    /// <returns>The compile errors with their line and column in the body; empty when it compiles.</returns>
    public IReadOnlyList<CodeProblem> Check(string body);
}
