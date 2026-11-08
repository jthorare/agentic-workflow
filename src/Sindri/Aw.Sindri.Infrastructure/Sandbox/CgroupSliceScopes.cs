using System.Globalization;
using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>ISliceScopes over the cgroup v2 tree: each live scope is a '*.scope' directory under the slice's cgroup directory.</summary>
/// <param name="sliceDirectory">The slice's directory, for example '/sys/fs/cgroup/user.slice/user-1000.slice/user@1000.service/sindri.slice/sindri-steps.slice'.</param>
/// <param name="processName">The name of a process id (null when it is gone); defaults to /proc/&lt;pid&gt;/comm.</param>
internal sealed class CgroupSliceScopes(string sliceDirectory, Func<int, string?>? processName = null) : ISliceScopes
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
                [.. Directory.EnumerateDirectories(sliceDirectory, "*.scope").Where(IsLive).Select(d => Path.GetFileName(d)).Order(StringComparer.Ordinal)];
            return Task.FromResult(scopes);
        }
        catch (DirectoryNotFoundException)
        {
            // The slice went away between the check and the listing: no scopes.
            return Task.FromResult<IReadOnlyList<string>>([]);
        }
    }

    // why: a killed sandbox init leaves its two socat forwarders in the scope; they must not hold a session slot.
    // A scope with no readable cgroup.procs can't be judged, so it counts. A pid with no name has exited.
    private bool IsLive(string scope)
    {
        string[] pids;
        try
        {
            pids = File.ReadAllLines(Path.Combine(scope, "cgroup.procs"));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return true;
        }

        var name = processName ?? ProcComm;
        return pids.Any(p => int.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out var pid)
            && name(pid) is { } n && !string.Equals(n, "socat", StringComparison.Ordinal));
    }

    private static string? ProcComm(int pid)
    {
        try
        {
            return File.ReadAllText(string.Create(CultureInfo.InvariantCulture, $"/proc/{pid}/comm")).Trim();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
