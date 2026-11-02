using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Testing.Launch;

/// <summary>An <see cref="IModelPickHistory"/> in memory: answers the rows a test gave it for the models asked about.</summary>
public sealed class FakeModelPickHistory : IModelPickHistory
{
    private readonly List<ModelPickHistoryRow> _rows = [];

    /// <summary>The rows every call draws from.</summary>
    public IList<ModelPickHistoryRow> Rows => _rows;

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<ModelPickHistoryRow>> For(RecipeId recipe, int version, string state, IReadOnlyList<string> models, CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<ModelPickHistoryRow>>([.. _rows.Where(r => models.Contains(r.Model, StringComparer.Ordinal))]);
}
