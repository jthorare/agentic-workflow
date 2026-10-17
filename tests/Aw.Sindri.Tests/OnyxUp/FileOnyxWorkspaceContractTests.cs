using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Host;
using Aw.Sindri.Infrastructure.Onyx;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxUp;

// The file adapter over a real temp directory.
[Lane(TestLane.Integration)]
public sealed class FileOnyxWorkspaceContractTests : OnyxWorkspaceContract
{
    protected override OnyxWorkspaceRig Create() => new FileRig();

    private sealed class FileRig : OnyxWorkspaceRig
    {
        private readonly string _home = Path.Combine(Path.GetTempPath(), "aw-onyx-ws-" + Guid.NewGuid().ToString("N"));

        public FileRig()
        {
            Paths = OnyxPaths.For(_home);
            Workspace = new FileOnyxWorkspace(new FakeSindriHome(_home), AwJson.CreateOptions(SindriJsonContext.Default));
        }

        public override IOnyxWorkspace Workspace { get; }

        public override OnyxPaths Paths { get; }

        public override void WriteRawState(string text)
        {
            _ = Directory.CreateDirectory(Paths.Root);
            File.WriteAllText(Paths.State, text);
        }

        public override void Dispose()
        {
            if (Directory.Exists(_home))
            {
                Directory.Delete(_home, recursive: true);
            }

            base.Dispose();
        }
    }
}
