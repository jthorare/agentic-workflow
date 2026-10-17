using System.Globalization;
using Aw.Kernel;

namespace Aw.Sindri.Core.Onyx;

/// <summary>The rule that Sindri binds Onyx to loopback and nothing else: judged on the rendered compose before `up` starts anything.</summary>
public static class OnyxBindings
{
    private const string Loopback = "127.0.0.1";

    /// <summary>Checks that the only published port across all services is nginx's 80 on 127.0.0.1 at the chosen port.</summary>
    /// <param name="config">What the rendered compose publishes and which services reach the host another way.</param>
    /// <param name="port">The loopback port Sindri chose.</param>
    /// <returns>Null when the bindings are exactly the expected one and no service has the host's network, pid namespace or privileges; else SND-ONYX-004 naming the first service and setting (or the missing nginx binding).</returns>
    public static Error? Verify(ComposeConfigFacts config, int port)
    {
        ArgumentNullException.ThrowIfNull(config);
        var published = config.Ports;
        if (config.HostAccess.Count > 0)
        {
            var access = config.HostAccess[0];
            return SindriErrors.OnyxPinsMalformed.With(string.Create(CultureInfo.InvariantCulture, $"the rendered compose gives {access.Service} the host's network, processes or privileges ({access.Setting}); Sindri only runs Onyx isolated from the host (nginx on {Loopback}:{port} is the one way in), so nothing was started: the pinned compose or Sindri's override changed, update Sindri and context/onyx.lock together"));
        }

        var expected = (Service: "nginx", HostIp: Loopback, Published: port, Target: 80);
        var other = published.FirstOrDefault(p => (p.Service, p.HostIp, p.Published, p.Target) != expected);
        if (other is not null)
        {
            var address = other.HostIp.Length == 0 ? "every interface" : other.HostIp;
            return SindriErrors.OnyxPinsMalformed.With(string.Create(CultureInfo.InvariantCulture, $"the rendered compose publishes {other.Service} on {address}:{other.Published} (container port {other.Target}); Sindri only binds Onyx to loopback (nginx on {Loopback}:{port}), so nothing was started: the pinned compose or Sindri's override changed, update Sindri and context/onyx.lock together"));
        }

        return published.Count == 1
            ? null
            : SindriErrors.OnyxPinsMalformed.With(string.Create(CultureInfo.InvariantCulture, $"the rendered compose doesn't publish nginx on {Loopback}:{port}, which is how Onyx is reached; nothing was started: the pinned compose or Sindri's override changed, update Sindri and context/onyx.lock together"));
    }
}
