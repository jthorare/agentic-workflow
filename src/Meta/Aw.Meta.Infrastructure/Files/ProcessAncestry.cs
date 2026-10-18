namespace Aw.Meta.Infrastructure.Files;

/// <summary>The parent chain of a process, shared by the /proc scan (Linux) and the lsof scan (macOS).</summary>
internal static class ProcessAncestry
{
    /// <summary>The process and each of its ancestors, up to but not including init (pid 1). A cycle in the parent map ends the walk.</summary>
    /// <param name="start">The process the walk starts from.</param>
    /// <param name="parents">Each known pid's parent pid.</param>
    /// <returns>The pids of the chain, the start included.</returns>
    internal static HashSet<int> Chain(int start, IReadOnlyDictionary<int, int> parents)
    {
        ArgumentNullException.ThrowIfNull(parents);
        var chain = new HashSet<int>();
        for (var pid = start; pid > 1 && chain.Add(pid) && parents.TryGetValue(pid, out var parent); pid = parent)
        {
            // each ancestor, up to init
        }

        return chain;
    }
}
