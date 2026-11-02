using Aw.Kernel.Testing;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>What is still running with a path in its command line: the teardown check of the heavy sandbox proofs.</summary>
internal static class ProcessListing
{
    /// <summary>Gets the command lines of the processes that name <paramref name="root"/>, after giving the ones just ending a moment to go.</summary>
    /// <param name="root">The directory the proof ran under.</param>
    /// <returns>The command lines; empty when nothing is left.</returns>
    internal static List<string> Under(string root)
    {
        List<string> left = [];
        for (var attempt = 0; attempt < 50; attempt++)
        {
            left = [.. Directory.GetDirectories("/proc").Where(static d => int.TryParse(Path.GetFileName(d), out _)).Select(CommandLineOf).Where(c => c.Contains(root, StringComparison.Ordinal))];
            if (left.Count == 0)
            {
                break;
            }

            RealTime.Sleep(TimeSpan.FromMilliseconds(100));
        }

        return left;
    }

    private static string CommandLineOf(string procDir)
    {
        try
        {
            return File.ReadAllText(procDir + "/cmdline").Replace('\0', ' ');
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return string.Empty;   // the process ended while we looked
        }
    }
}
