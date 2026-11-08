using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Stub <see cref="IItems"/> until API P3 (items) lands (engine P4 contract): an item-shaped reference ('ITM-…', 'T-1')
/// resolves to itself with no aliases. API P3's store adapter replaces it.
/// </summary>
internal sealed class SelfItems : IItems
{
    public ValueTask<Result<ResolvedItem>> Resolve(string reference, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<ResolvedItem>>(ItemKey.TryFrom(reference, out var item)
            ? new ResolvedItem(item, [])
            : SindriErrors.InvalidInput.With($"'{reference}' is not an item id or tracker key ('ITM-…', 'T-3')"));
}
