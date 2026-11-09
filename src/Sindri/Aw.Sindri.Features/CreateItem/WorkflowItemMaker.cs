using Aw.Kernel;
using Aw.Sindri.Core.Graph.Blocks;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.CreateItem;

/// <summary><see cref="ICreateItemPort"/> over the create-item handler: a 'create_item' node makes its item the way 'sindri item add' does, as 'service:workflow'.</summary>
/// <param name="scopes">Makes a scope per item; the handler is scoped and the engine's blocks are not.</param>
internal sealed class WorkflowItemMaker(IServiceScopeFactory scopes) : ICreateItemPort
{
    public ValueTask<Result<string>> Create(string title, string? repo, string? body, string? linearIssue, CancellationToken cancellationToken)
    {
        _ = scopes;
        throw new NotSupportedException("WorkflowItemMaker is not written yet");
    }
}
