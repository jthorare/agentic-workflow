using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Tests.ContextCalls.Routing;

/// <summary>Codex's model list as fixed slugs; counts the reads.</summary>
internal sealed class FixedCodexList(string[] codex) : IModelCatalog
{
    public int Reads { get; private set; }

    public ValueTask<IReadOnlyList<ModelFacts>> ModelsOf(AgentId agent, CancellationToken cancellationToken)
    {
        Reads++;
        return ValueTask.FromResult<IReadOnlyList<ModelFacts>>(string.Equals(agent.Value, "codex", StringComparison.Ordinal) ? [.. codex.Select(static slug => new ModelFacts(slug, slug, Aw.Sindri.Core.Launch.ModelSource.Discovered))] : []);
    }
}
