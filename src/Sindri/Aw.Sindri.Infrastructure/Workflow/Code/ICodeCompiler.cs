namespace Aw.Sindri.Infrastructure.Workflow.Code;

/// <summary>Compiles the body of a Code node (the seam the cache and its tests use).</summary>
internal interface ICodeCompiler
{
    /// <summary>Names the reference set the compiler builds against; it is part of the cache key, so a change of references recompiles.</summary>
    public string ReferenceSet { get; }

    /// <summary>Compiles the body as a static method that gets 'JsonNode? input' and returns any value.</summary>
    /// <param name="body">The method body.</param>
    /// <returns>The assembly, or the errors with their line and column in the body.</returns>
    public CodeCompilation Compile(string body);
}
