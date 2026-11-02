using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>ISliceScopes over the cgroup v2 tree: each live scope is a '*.scope' directory under the slice's cgroup directory.</summary>
/// <param name="sliceDirectory">The slice's directory, for example '/sys/fs/cgroup/user.slice/user-1000.slice/user@1000.service/sindri.slice/sindri-steps.slice'.</param>
internal sealed class CgroupSliceScopes(string sliceDirectory) : ISliceScopes
{
    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> Live(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(sliceDirectory))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        try
        {
            IReadOnlyList<string> scopes =
                [.. Directory.EnumerateDirectories(sliceDirectory, "*.scope").Select(d => Path.GetFileName(d)).Order(StringComparer.Ordinal)];
            return Task.FromResult(scopes);
        }
        catch (DirectoryNotFoundException)
        {
            // The slice went away between the check and the listing: no scopes.
            return Task.FromResult<IReadOnlyList<string>>([]);
        }
    }
}
