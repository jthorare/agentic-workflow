using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Launch;

/// <summary>What the launcher's probe records (launcher L-4, L-5): the terminal and host facts, each target's source, override and limits.</summary>
[Lane(TestLane.Unit)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class LauncherProbeTests : IDisposable
{
    private readonly string _home = Directory.CreateTempSubdirectory("aw-probe-").FullName;

    public void Dispose() => Directory.Delete(_home, recursive: true);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private Launcher Make(IReadOnlyDictionary<string, IUsageReader> readers, params AgentEntry[] entries) =>
        new(new LauncherParts(new AgentCatalog(entries), _ => new FakeSessionHost(), new FakeTerminalViewer(), new LauncherOptions(_home), new FakeTimeProvider(KnownInstants.Epoch))
        {
            ResolveBin = static (bin, _) => ValueTask.FromResult(string.Equals(bin, "agent", StringComparison.Ordinal) ? null : "/usr/bin/" + bin),
            Usage = name => readers.GetValueOrDefault(name),
        });

    [Fact]
    public async Task Probed_has_L4_fields()
    {
        var readers = new Dictionary<string, IUsageReader>(StringComparer.Ordinal)
        {
            ["claude-code"] = new FakeUsageReader("claude-code"),
            ["cursor"] = new FakeUsageReader("cursor", verified: false),
        };
        var probed = await Make(readers, AgentEntries.ClaudeCode, AgentEntries.Cursor, AgentEntries.Aider).Probe(Token);

        Expect.Equal(HostKind.Tmux, probed.Host.Kind);
        Expect.Equal("3.4", probed.Host.Version);
        var byId = probed.Targets.ToDictionary(static t => t.Id.Value, StringComparer.Ordinal);
        Expect.Equal((true, null, CatalogSource.Builtin, false), (byId["claude-code"].Present, byId["claude-code"].Limited, byId["claude-code"].Source, byId["claude-code"].Overridden));
        Expect.Equal("usage-unverified", byId["cursor"].Limited);   // API P10a Q11: no CLI to check the reader against
        Expect.Equal(((string?)"not-installed", false), (byId["cursor"].Problem, byId["cursor"].Present));
        Expect.Equal(("no-usage-reader", CatalogSource.Added), (byId["aider"].Limited, byId["aider"].Source));
    }

    [Fact]
    public async Task An_overridden_entry_is_flagged()
    {
        var entry = new AgentEntry(AgentId.From("claude-code"), "Mine", "claude", ProcessName: null, Transcripts: null, Resume: null) { Source = CatalogSource.Override, Usage = "none" };
        var probed = await Make(new Dictionary<string, IUsageReader>(StringComparer.Ordinal), entry).Probe(Token);

        var target = Assert.Single(probed.Targets);
        Expect.Equal((true, CatalogSource.Override, "no-usage-reader"), (target.Overridden, target.Source, target.Limited));
    }
}
