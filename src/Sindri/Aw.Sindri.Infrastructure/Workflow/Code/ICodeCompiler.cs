namespace Aw.Sindri.Infrastructure.Workflow.Code;

/// <summary>Compiles the body of a Code node (the seam the cache and its tests use).</summary>
internal interface ICodeCompiler
{
    /// <summary>Names what the compiler builds against: the reference set and the wrapper around the body. It is part of the cache key, so a change of either recompiles.</summary>
    public string Fingerprint { get; }

    /// <summary>Compiles the body as a static method that gets 'JsonNode? input' and returns any value.</summary>
    /// <param name="body">The method body.</param>
    /// <param name="cancellationToken">Stops the compile.</param>
    /// <returns>The assembly, or the errors with their line and column in the body.</returns>
    public CodeCompilation Compile(string body, CancellationToken cancellationToken);
}
