using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Infrastructure.Workflow.Code;

/// <summary>The validator's compile check: the same compiler the runner's cache uses, with nothing run and nothing cached.</summary>
/// <param name="compiler">Compiles the body.</param>
internal sealed class CodeChecker(ICodeCompiler compiler) : ICodeCheck
{
    /// <inheritdoc />
    public IReadOnlyList<CodeProblem> Check(string body) => [.. compiler.Compile(body).Diagnostics.Select(static d => new CodeProblem(d.Line, d.Column, d.Id, d.Message))];
}
