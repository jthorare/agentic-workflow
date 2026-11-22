using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>A chain of code steps as the server serves it: 280 px apart in one row, so the canvas has to lay the 290 px cards out itself (#3684).</summary>
internal static class ChainGraph
{
    /// <summary>Builds a chain s0 to s{count-1}.</summary>
    /// <param name="count">The number of steps.</param>
    /// <returns>The graph.</returns>
    internal static Task<WorkflowGraphViewModel> Of(int count)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var ids = Enumerable.Range(0, count).Select(i => string.Create(inv, $"s{i}")).ToList();
        var nodes = "[" + string.Join(',', ids.Select(id => "{\"id\":\"" + id + "\",\"type\":\"code\",\"title\":\"" + id + "\",\"config\":{}}")) + "]";
        var edges = "[" + string.Join(',', ids.Skip(1).Select((id, i) => "{\"from\":\"" + ids[i] + "\",\"to\":\"" + id + "\"}")) + "]";
        var positions = "[" + string.Join(',', ids.Select((id, i) => "{\"node\":\"" + id + "\",\"x\":" + (i * 280).ToString(inv) + ",\"y\":0}")) + "]";
        return WorkflowGraphViewModelTests.GraphOf(new WorkflowsFixture(), nodes, edges, positions);
    }
}
