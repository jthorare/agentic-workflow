using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The model port of a build whose host registers none: a 'prompt' node fails with the reason.</summary>
internal sealed class NoGraphModelPort : IGraphModelPort
{
    /// <inheritdoc />
    public ValueTask<Result<string>> Ask(string prompt, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<string>>(SindriErrors.GraphBlockFailed.With("this build has no model to answer a 'prompt' node"));
}
