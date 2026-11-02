using Aw.Kernel;
using Aw.Sindri.Core.Leftovers;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Infrastructure.Leftovers;

namespace Aw.Sindri.Tests.OnyxPurge;

// A throwaway Sindri home, a docker with labelled and unlabelled things in it and a keychain, all recording what they were asked to do.
internal sealed class OnyxPurgeRig : IDisposable
{
    internal const string Label = "sindri-onyx-0a1b2c3d";

    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    internal OnyxPurgeRig()
    {
        Home = Path.Combine(Path.GetTempPath(), "aw-onyx-purge-" + Path.GetRandomFileName());
        _ = Directory.CreateDirectory(Home);
    }

    internal string Home { get; }

    internal string OnyxDirectory => Path.Combine(Home, "onyx");

    internal FakeDocker Docker { get; } = new();

    internal FakeKeychain Keychain { get; } = new();

    internal List<string> Calls { get; } = [];

    internal string Out => _out.ToString();

    internal string Error => _error.ToString();

    internal void WriteState(string json)
    {
        _ = Directory.CreateDirectory(OnyxDirectory);
        File.WriteAllText(Path.Combine(OnyxDirectory, "state.json"), json);
        File.WriteAllText(Path.Combine(OnyxDirectory, ".env"), "SECRET=1");
    }

    internal Task<int> Run(bool human, string? answer)
    {
        Docker.Calls = Calls;
        Keychain.Calls = Calls;
        var command = new OnyxPurgeCommand(Home, Docker, Keychain, new DiskLeftoverFiles(), new Output(_out, _error, SliceHarness.Json), new StringReader(answer is null ? "" : answer + "\n"), _error);
        return command.Run(human, TestContext.Current.CancellationToken);
    }

    public void Dispose()
    {
        if (Directory.Exists(Home))
        {
            Directory.Delete(Home, recursive: true);
        }
    }

    internal sealed class FakeDocker : IPurgeDocker
    {
        internal List<string> Calls { get; set; } = [];

        // name -> compose project label (null: no label)
        internal Dictionary<string, string?> Containers { get; } = new(StringComparer.Ordinal);

        internal Dictionary<string, string?> Volumes { get; } = new(StringComparer.Ordinal);

        public Result<IReadOnlyList<string>> ContainersOf(string project) => Labelled(Containers, project);

        public Result<IReadOnlyList<string>> VolumesOf(string project) => Labelled(Volumes, project);

        public Result<Unit> RemoveContainers(IReadOnlyList<string> names)
        {
            foreach (var name in names)
            {
                Calls.Add("rm " + name);
                _ = Containers.Remove(name);
            }

            return Results.Ok();
        }

        public Result<Unit> RemoveVolumes(IReadOnlyList<string> names)
        {
            foreach (var name in names)
            {
                Calls.Add("volume rm " + name);
                _ = Volumes.Remove(name);
            }

            return Results.Ok();
        }

        private static Result<IReadOnlyList<string>> Labelled(Dictionary<string, string?> things, string project)
            => things.Where(t => t.Value == project).Select(t => t.Key).Order(StringComparer.Ordinal).ToList();
    }

    internal sealed class FakeKeychain : IPurgeKeychain
    {
        internal List<string> Calls { get; set; } = [];

        public Result<Unit> Remove(string service, string account)
        {
            Calls.Add($"keychain {service}/{account}");
            return Results.Ok();
        }
    }
}
