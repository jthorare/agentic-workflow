using Aw.Context.Core.Engine;
using Aw.Sindri.Host.Models;

namespace Aw.Sindri.Tests.ContextCalls.Routing;

/// <summary>The approved 'context.models', fixed.</summary>
internal sealed class FixedModels(ModelsSettings settings) : IApprovedModels
{
    public ValueTask<ModelsSettings> Current(CancellationToken cancellationToken) => ValueTask.FromResult(settings);
}
