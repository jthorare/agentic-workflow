using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Infrastructure.Workflow.Code;

/// <summary>The validator's compile check: the runner's compiled-code cache, with nothing run.</summary>
/// <param name="cache">Compiles the body under the run's key.</param>
internal sealed class CodeChecker(CompiledCodeCache cache) : ICodeCheck
{
    /// <inheritdoc />
    public IReadOnlyList<CodeProblem> Check(string body, CancellationToken cancellationToken) =>
        [.. cache.Check(body, cancellationToken).Select(static d => new CodeProblem(d.Line, d.Column, d.Id, d.Message))];
}
