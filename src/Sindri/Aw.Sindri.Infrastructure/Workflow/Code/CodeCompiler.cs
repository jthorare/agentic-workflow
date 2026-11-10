// why: the red commit holds stubs only; the registration that uses this type comes with the implementation
#pragma warning disable MA0182
namespace Aw.Sindri.Infrastructure.Workflow.Code;

/// <summary>Roslyn over a fixed reference set: the base class library, System.Text.Json and System.Linq.</summary>
internal sealed class CodeCompiler : ICodeCompiler
{
    /// <inheritdoc />
    public string ReferenceSet => throw new NotSupportedException();

    /// <inheritdoc />
    public CodeCompilation Compile(string body) => throw new NotSupportedException();
}
