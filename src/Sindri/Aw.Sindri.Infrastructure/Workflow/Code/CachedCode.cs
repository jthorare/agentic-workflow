namespace Aw.Sindri.Infrastructure.Workflow.Code;

/// <summary>A body resolved through the cache.</summary>
/// <param name="AssemblyPath">The cached assembly file, or null when the body does not compile.</param>
/// <param name="Diagnostics">The compile errors; empty when the assembly exists.</param>
internal sealed record CachedCode(string? AssemblyPath, IReadOnlyList<CodeDiagnostic> Diagnostics);
