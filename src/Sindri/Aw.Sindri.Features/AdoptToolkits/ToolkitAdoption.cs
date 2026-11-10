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
    internal async ValueTask<Result<int>> Run(CancellationToken cancellationToken)
    {
        var all = await store.List(cancellationToken).ConfigureAwait(false);
        if (all.IsError)
        {
            return all.Error;
        }

        var known = all.Value.Select(static t => t.Name).ToHashSet(StringComparer.Ordinal);
        var adopted = 0;
        foreach (var folder in folders.Folders().Where(f => ToolNames.IsToolkitName(f.Name) && !known.Contains(f.Name)))
        {
            var saved = await store.Save(new Toolkit(folder.Name, folder.Dir, ToolkitState.Off, SourceHash: null, BuiltAt: null, Manifest: null, Adopter), cancellationToken).ConfigureAwait(false);
            if (saved.IsError)
            {
                return saved.Error;
            }

            adopted++;
        }

        return adopted;
    }
}
