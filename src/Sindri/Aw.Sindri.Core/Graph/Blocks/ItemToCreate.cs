using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>What a 'create_item' node asks <see cref="ICreateItemPort"/> to make.</summary>
/// <param name="Run">The run whose node asks; the item becomes its item.</param>
/// <param name="Title">The title.</param>
/// <param name="Repo">The repo's name, or null for none.</param>
/// <param name="Body">The body, or null.</param>
/// <param name="LinearIssue">The Linear issue it comes from as 'linear:issue/&lt;id&gt;', or null.</param>
public sealed record ItemToCreate(WorkflowRunId Run, string Title, string? Repo, string? Body, string? LinearIssue);
