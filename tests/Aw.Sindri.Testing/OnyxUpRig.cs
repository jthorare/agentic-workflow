using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Infrastructure.Onyx;

namespace Aw.Sindri.Testing;

/// <summary>Every fake `onyx up` runs against, wired as in production: the real pins and the real v4.9.0 files as the remote, a runtime that answers like a healthy Mac, and a clock that only moves when the test says.</summary>
public sealed class OnyxUpRig
{
    /// <summary>The Sindri home the rig runs in.</summary>
    public const string Home = "/sindri-home";

    /// <summary>Initializes the rig with healthy defaults.</summary>
    public OnyxUpRig()
    {
        Pins = new FakeOnyxPins(new EmbeddedOnyxPins().Pins);
        Runtime = new FakeContainerRuntime(FakeContainerRuntime.Healthy);
        Health = new FakeOnyxHealth { Default = new OnyxProbe(Healthy: true, Pins.Pins.Version) };
        Probes = new FakeHostProbes();
        _ = Probes.Directories.Add(Home);
        Paths = OnyxPaths.For(Home);
        Files = new FakeDeployFiles(
            Pins.Pins.Files.ToDictionary(static f => f.Path, static f => OnyxFixtures.ReadBytes(f.Path), StringComparer.Ordinal),
            new Dictionary<string, byte[]>(StringComparer.Ordinal))
        {
            Written = Mirror,
        };
        Runtime.Config = _ => [new ComposePublishedPort("nginx", "127.0.0.1", RenderedPort(), 80)];
        Random = new FakeRandomHex();
        Clock = new RigClock();
        foreach (var pin in Pins.Pins.Images.Where(static i => i.Platform == "linux/arm64"))
        {
            Runtime.LocalImages[pin.Repository + "@" + pin.Digest] = [ImageNames.WithoutDefaults(pin.Repository + "@" + pin.Digest)];
        }
    }

    /// <summary>Gets the swappable pins.</summary>
    public FakeOnyxPins Pins { get; }

    /// <summary>Gets the container runtime.</summary>
    public FakeContainerRuntime Runtime { get; }

    /// <summary>Gets the readiness probe; healthy with the pinned version unless a test changes it.</summary>
    public FakeOnyxHealth Health { get; }

    /// <summary>Gets the host probes (home at /sindri-home with plenty of disk; every port free).</summary>
    public FakeHostProbes Probes { get; }

    /// <summary>Gets the workspace.</summary>
    public FakeOnyxWorkspace Workspace { get; } = new();

    /// <summary>Gets the deploy-file fetch; a downloaded file also lands in <see cref="Workspace"/> at its path.</summary>
    public FakeDeployFiles Files { get; }

    /// <summary>Gets the remote's files by path under deployment/ (mutate to serve something else).</summary>
    public IDictionary<string, byte[]> Remote => Files.Remote;

    /// <summary>Gets the random source.</summary>
    public FakeRandomHex Random { get; }

    /// <summary>Gets the clock.</summary>
    public RigClock Clock { get; }

    /// <summary>Gets the paths under the home.</summary>
    public OnyxPaths Paths { get; }

    /// <summary>Gets what the service reported.</summary>
    public CapturingOnyxProgress Progress { get; } = new();

    // Compose reads HOST_PORT from the rendered .env, so the fake config answers with it (3189 before the env exists).
    private int RenderedPort()
        => (Workspace.Read(Paths.Env) is { IsSuccess: true } read ? read.Value : null)?.Split('\n').FirstOrDefault(static l => l.StartsWith("HOST_PORT=", StringComparison.Ordinal)) is { } line
            && int.TryParse(line["HOST_PORT=".Length..], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var port)
                ? port
                : 3189;

    // A file the fake fetch "wrote" is on disk for the workspace too.
    private void Mirror(string path, byte[] bytes) => Workspace.Files[Paths.DeployDir + "/" + path] = System.Text.Encoding.UTF8.GetString(bytes);
}
