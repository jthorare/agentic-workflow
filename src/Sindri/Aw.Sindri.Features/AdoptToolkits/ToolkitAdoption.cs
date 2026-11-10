using Aw.Kernel;
using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Features.AdoptToolkits;

/// <summary>Gives every toolkit folder under the toolkits root that has no row a row in state 'off', created by 'adopted'. It runs at serve start.</summary>
/// <param name="store">The toolkit store.</param>
/// <param name="folders">The toolkits root.</param>
internal sealed class ToolkitAdoption(IToolkitStore store, IToolkitFolders folders)
{
    /// <summary>The creator an adopted row records.</summary>
    internal const string Adopter = "adopted";

    /// <summary>Adds the missing rows.</summary>
    /// <param name="cancellationToken">Cancels the pass.</param>
    /// <returns>How many rows it added, or the store's error.</returns>
    internal ValueTask<Result<int>> Run(CancellationToken cancellationToken)
    {
        _ = (store, folders, cancellationToken);
        throw new NotSupportedException();
    }
}
