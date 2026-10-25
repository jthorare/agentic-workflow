using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Host.Commands.SessionWrap;

/// <summary>The real limits source: the JSON file the launcher writes beside the plan, and a systemd scope control for its unit.</summary>
internal sealed class WrapLimitsFile : IWrapLimitsSource
{
    private static readonly System.Text.Json.JsonSerializerOptions _json = AwJson.CreateOptions(SindriJsonContext.Default);

    /// <inheritdoc />
    public Result<WrapLimitsSetup> Open(string path)
    {
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SindriErrors.InvalidInput.With($"the limits file {path} cannot be read: {e.Message}");
        }

        var read = AwJson.Deserialize<WrapLimits>(bytes, _json);
        if (read.IsError)
        {
            return SindriErrors.InvalidInput.With($"the limits file {path} is not valid: {read.Error.Message}");
        }

        var limits = read.Value;
        return string.IsNullOrWhiteSpace(limits.Unit)
            ? SindriErrors.InvalidInput.With($"the limits file {path} names no scope unit")
            : new WrapLimitsSetup(limits, new SystemdScope(limits.Unit, new SandboxProcesses(), new HostPaths()));
    }
}
