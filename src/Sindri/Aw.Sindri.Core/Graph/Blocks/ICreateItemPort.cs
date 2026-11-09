using Aw.Kernel;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>Where a 'create_item' node's item is made: the same create-item handler 'sindri item add' and the app's New item form use.</summary>
public interface ICreateItemPort
{
    /// <summary>Creates the item.</summary>
    /// <param name="run">The run whose node asks; the item becomes its item.</param>
    /// <param name="title">The title.</param>
    /// <param name="repo">The repo's name, or null for none.</param>
    /// <param name="body">The body, or null.</param>
    /// <param name="linearIssue">The Linear issue it comes from as 'linear:issue/&lt;id&gt;', or null.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The new item's id, or why the item was not made.</returns>
    public ValueTask<Result<string>> Create(WorkflowRunId run, string title, string? repo, string? body, string? linearIssue, CancellationToken cancellationToken);
}
