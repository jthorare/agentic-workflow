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

    private Launcher Make(IReadOnlyDictionary<string, IUsageReader> readers, params AgentEntry[] entries) => Make(readers, models: null, entries);

    private Launcher Make(IReadOnlyDictionary<string, IUsageReader> readers, IModelCatalog? models, params AgentEntry[] entries) =>
        new(new LauncherParts(new AgentCatalog(entries), _ => new FakeSessionHost(), new FakeTerminalViewer(), new LauncherOptions(_home), new FakeTimeProvider(KnownInstants.Epoch))
        {
            Models = models,
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

    private static readonly Dictionary<string, IUsageReader> _verified = new(StringComparer.Ordinal) { ["claude-code"] = new FakeUsageReader("claude-code") };

    [Fact]
    public async Task A_target_carries_the_models_its_agent_offers()
    {
        var models = new FakeModelCatalog().With(AgentId.From("claude-code"), [new ModelFacts("haiku", "Haiku", ModelSource.Catalog, 0), new ModelFacts("opus", "Opus", ModelSource.Catalog, 1)]);

        var probed = await Make(_verified, models, AgentEntries.ClaudeCode).Probe(Token);

        var target = Assert.Single(probed.Targets);
        Expect.Equal("haiku,opus", string.Join(',', target.Models!.Select(static m => m.Slug)));
        Expect.True(target.Limited is null);
    }

    [Fact]
    public async Task An_installed_agent_with_no_readable_list_says_no_model_list()
    {
        var probed = await Make(_verified, new FakeModelCatalog(), AgentEntries.ClaudeCode).Probe(Token);

        var target = Assert.Single(probed.Targets);
        Expect.Equal("no-model-list", target.Limited);
        Expect.Equal(0, target.Models!.Count);
    }

    [Fact]
    public async Task No_model_list_joins_the_other_limits()
    {
        var probed = await Make(new Dictionary<string, IUsageReader>(StringComparer.Ordinal), new FakeModelCatalog(), AgentEntries.ClaudeCode).Probe(Token);

        Expect.Equal("no-usage-reader; no-model-list", Assert.Single(probed.Targets).Limited);
    }

    [Fact]
    public async Task A_model_with_no_place_is_named_as_unordered()
    {
        var models = new FakeModelCatalog().With(AgentId.From("claude-code"), [new ModelFacts("haiku", "Haiku", ModelSource.Catalog, 0), new ModelFacts("mystery", "Mystery", ModelSource.Discovered)]);

        var probed = await Make(_verified, models, AgentEntries.ClaudeCode).Probe(Token);

        Expect.Equal("unordered: mystery", Assert.Single(probed.Targets).Limited);
    }

    [Fact]
    public async Task An_agent_that_is_not_installed_is_not_asked_for_models()
    {
        var entry = new AgentEntry(AgentId.From("agent"), "Agent", "agent", ProcessName: null, Transcripts: null, Resume: null) { Usage = "none" };
        var models = new FakeModelCatalog().With(entry.Id, [new ModelFacts("a", "A", ModelSource.Catalog, 0)]);

        var probed = await Make(new Dictionary<string, IUsageReader>(StringComparer.Ordinal), models, entry).Probe(Token);

        var target = Assert.Single(probed.Targets);
        Expect.Equal("not-installed", target.Problem);
        Expect.Equal(0, target.Models!.Count);
        Expect.Equal("no-usage-reader", target.Limited);
    }
}
