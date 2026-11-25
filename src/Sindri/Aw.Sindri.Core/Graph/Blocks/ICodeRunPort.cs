using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>Where a 'code' node's C# is compiled and run: the compiler, the cache and the sandboxed runner process live behind it.</summary>
public interface ICodeRunPort
{
    /// <summary>Compiles the body (or takes it from the cache) and runs it.</summary>
    /// <param name="run">The body, its input and its limits.</param>
    /// <param name="cancellationToken">Cancels the run and kills the process.</param>
    /// <returns>The result as JSON, or SND-CODE-001 (does not compile), -002 (timeout), -003 (memory), -004 (threw) or -005 (the runner could not start).</returns>
    public ValueTask<Result<string>> Run(CodeRun run, CancellationToken cancellationToken);
}
