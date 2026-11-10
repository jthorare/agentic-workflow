// why: the red commit holds stubs only; the registration that uses this type comes with the implementation
#pragma warning disable MA0182
namespace Aw.Sindri.Infrastructure.Workflow.Code;

/// <summary>Compiled assemblies on disk, keyed by the sha256 of the body and the compiler's reference set, so the same code compiles once.</summary>
/// <param name="compiler">Compiles a body that is not cached.</param>
/// <param name="directory">The folder the assemblies live in.</param>
internal sealed class CompiledCodeCache(ICodeCompiler compiler, string directory)
{
    /// <summary>Gives the assembly for the body, compiling it only when it is not cached.</summary>
    /// <param name="body">The method body.</param>
    /// <returns>The assembly's path, or the compile errors.</returns>
    public CachedCode Resolve(string body) => throw new NotSupportedException($"{compiler.ReferenceSet} {directory}");
}
