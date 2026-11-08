using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ForgetMemoryFact;

/// <summary>Forget facts ('sindri memory forget &lt;name&gt; [--fact KEY | --tag T | --expired]'; DELETE /sindri/memories/{name}/facts; MCP memory_forget). Exactly one selector.</summary>
/// <param name="Name">The memory.</param>
/// <param name="Fact">One fact's key, or null.</param>
/// <param name="Tag">Every fact with this tag, or null.</param>
/// <param name="Expired">True: every fact that has expired.</param>
public sealed record ForgetMemoryFactRequest(string Name, string? Fact, string? Tag, bool Expired) : ICommand<ForgetMemoryFactResponse>;
