using System.Text;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Infrastructure.Onyx;

// `docker compose config --format json`: what the rendered files would publish, read before anything starts.
internal sealed partial class DockerCliRuntime
{
    /// <inheritdoc />
    public Result<ComposeConfigFacts> RenderedConfig(ComposeProject project, CancellationToken cancellationToken)
        => Compose(project, ["config", "--format", "json"], new Call("compose config", _shortTimeout), progress: null, cancellationToken).Bind(ParseConfig);

    // The output holds the whole environment (the auth key too), so nothing of it goes into an error.
    private static Result<ComposeConfigFacts> ParseConfig(string output)
    {
        var wire = AwJson.Deserialize<ComposeConfigWire>(Encoding.UTF8.GetBytes(output), _wire);
        if (wire.IsError || wire.Value.Services is null)
        {
            return SindriErrors.OnyxComposeFailed.With("`docker compose config --format json` printed something Sindri can't read; update Docker Compose to v2.24 or newer, then run this again");
        }

        return new ComposeConfigFacts(
            [.. wire.Value.Services
                .OrderBy(static s => s.Key, StringComparer.Ordinal)
                .SelectMany(static s => (s.Value.Ports ?? []).Select(p => new ComposePublishedPort(s.Key, p.HostIp ?? "", p.Published ?? 0, p.Target ?? 0)))],
            [.. wire.Value.Services
                .OrderBy(static s => s.Key, StringComparer.Ordinal)
                .SelectMany(static s => HostAccess(s.Key, s.Value))]);
    }

    private static IEnumerable<ComposeHostAccess> HostAccess(string service, ComposeConfigWire.ServiceWire wire)
    {
        if (wire.NetworkMode == "host")
        {
            yield return new ComposeHostAccess(service, "network_mode: host");
        }

        if (wire.Privileged == true)
        {
            yield return new ComposeHostAccess(service, "privileged: true");
        }

        if (wire.Pid == "host")
        {
            yield return new ComposeHostAccess(service, "pid: host");
        }
    }
}
