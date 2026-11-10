using Aw.Kernel;

// why: the red commit holds stubs only; the registration that uses this type comes with the implementation
#pragma warning disable MA0182
namespace Aw.Sindri.Infrastructure.Workflow.Code;

/// <summary>Runs a compiled body in a 'sindri-code-runner' process: the input goes in on stdin, the result comes back on stdout, and the process is killed at the timeout.</summary>
/// <param name="runner">The runner's command: the binary, or 'dotnet' and its dll.</param>
/// <param name="confine">Wraps the command in the sandbox, or null to run it as it is (tests).</param>
internal sealed class CodeRunnerProcess(IReadOnlyList<string> runner, Func<IReadOnlyList<string>, Result<IReadOnlyList<string>>>? confine = null)
{
    /// <summary>Runs the assembly's method on the input.</summary>
    /// <param name="assemblyPath">The cached assembly.</param>
    /// <param name="inputJson">The input as JSON.</param>
    /// <param name="limits">The timeout and the memory limit.</param>
    /// <param name="cancellationToken">Cancels the run and kills the process.</param>
    /// <returns>The result as JSON, or SND-CODE-002, -003, -004 or -005.</returns>
    public Task<Result<string>> Run(string assemblyPath, string inputJson, CodeLimits limits, CancellationToken cancellationToken) => throw new NotSupportedException($"{runner.Count} {confine is null}");
}
