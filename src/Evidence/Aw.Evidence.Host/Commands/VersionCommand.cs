using System.Reflection;

namespace Aw.Evidence.Host.Commands;

/// <summary>
/// The 'version' command prints the build stamp on one line, 'ui-evidence commit &lt;sha&gt;, built &lt;UTC ISO 8601&gt;' (either 'unknown' when the build
/// was not stamped), the shape of 'aw-meta version'. The stamp is Sindri's: the install publishes every binary with one SourceRevisionId and AwBuildTime.
/// It reads this assembly only: no browser, lock, stack or settings.
/// </summary>
internal static class VersionCommand
{
    internal const string Name = "version";
    internal const string Shape = "version";
    internal const string BuildTimeKey = "AwBuildTime";
    internal const string Unknown = "unknown";

    private static readonly CliSpec _spec = new(0, [], []);

    internal static Task<int> Run(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        if (CliArguments.Parse(args, _spec) is null)
        {
            return Task.FromResult(CommandOutput.Usage(Shape));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var assembly = typeof(VersionCommand).Assembly;
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty;
        var builtAt = assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(static a => a.Key == BuildTimeKey)?.Value;
        Console.Out.WriteLine(Stamp(version, builtAt));
        return Task.FromResult(CommandOutput.Ok);
    }

    /// <summary>The stamp line for an InformationalVersion ("1.0.0+&lt;commit&gt;" when the build passed SourceRevisionId) and a build time.</summary>
    /// <param name="informationalVersion">The assembly's InformationalVersion.</param>
    /// <param name="builtAt">The AwBuildTime metadata, or null.</param>
    /// <returns>The line, without a newline.</returns>
    internal static string Stamp(string informationalVersion, string? builtAt)
    {
        var plus = informationalVersion.IndexOf('+', StringComparison.Ordinal);
        var commit = plus >= 0 && plus + 1 < informationalVersion.Length ? informationalVersion[(plus + 1)..] : Unknown;
        return $"ui-evidence commit {commit}, built {(string.IsNullOrEmpty(builtAt) ? Unknown : builtAt)}";
    }
}
