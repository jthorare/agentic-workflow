using System.Reflection;
using System.Runtime.InteropServices;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Build;

/// <summary>
/// IBuildInfo over this assembly's attributes: the commit from its InformationalVersion (the publish passes SourceRevisionId) and
/// the build time from its AwBuildTime metadata (the publish passes AwBuildTime). A build that stamped neither reads "unknown".
/// </summary>
internal sealed class AssemblyBuildInfo : IBuildInfo
{
    internal const string BuildTimeKey = "AwBuildTime";

    public BuildInfo Read()
    {
        var assembly = typeof(AssemblyBuildInfo).Assembly;
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty;
        var builtAt = assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == BuildTimeKey)?.Value;
        return new BuildInfo(BuildStamp.Commit(version), string.IsNullOrEmpty(builtAt) ? BuildStamp.Unknown : builtAt, Os(), RuntimeInformation.RuntimeIdentifier);
    }

    private static string Os() =>
        OperatingSystem.IsMacOS() ? "macOS" : OperatingSystem.IsLinux() ? "Linux" : RuntimeInformation.OSDescription;
}
