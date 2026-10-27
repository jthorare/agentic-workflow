using Aw.Sindri.Core.Launch;
using Xunit;

namespace Aw.Sindri.Testing.Launch;

/// <summary>The contract every <see cref="IModelCatalog"/> honours, run against the fake now and against the probe-backed catalog when discovery lands: the models come back in the order given, and an agent with none gets an empty list.</summary>
public abstract class ModelCatalogContract
{
    private static readonly AgentId _agent = AgentId.From("claude-code");

    /// <summary>A catalog in which <paramref name="agent"/> offers exactly <paramref name="models"/> and every other agent offers none.</summary>
    /// <param name="agent">The agent.</param>
    /// <param name="models">Its models.</param>
    /// <returns>The catalog.</returns>
    protected abstract IModelCatalog Create(AgentId agent, IReadOnlyList<ModelFacts> models);

    /// <summary>The models come back in the order the source gave them.</summary>
    /// <returns>The task.</returns>
    [Fact]
    public async Task Models_come_back_in_the_order_given()
    {
        var catalog = Create(_agent, [Model("sonnet"), Model("haiku")]);

        var models = await catalog.ModelsOf(_agent, TestContext.Current.CancellationToken);

        Expect.Equal("sonnet,haiku", string.Join(',', models.Select(m => m.Slug)));
    }

    /// <summary>An agent the catalog knows nothing about offers no models, and the list is never null.</summary>
    /// <returns>The task.</returns>
    [Fact]
    public async Task An_agent_with_no_models_gets_an_empty_list()
    {
        var catalog = Create(_agent, [Model("haiku")]);

        var models = await catalog.ModelsOf(AgentId.From("codex"), TestContext.Current.CancellationToken);

        Expect.Equal(0, models.Count);
    }

    private static ModelFacts Model(string id) => new(id, id, ModelSource.Catalog);
}
