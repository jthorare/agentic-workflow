using Aw.Kernel.Json;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Sandbox.Denials;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Host;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Sandbox.Denials;

/// <summary>The denial counters on disk: one file per launch under the launcher's 'denials' folder, where the counter writes it.</summary>
public sealed class DiskDenialFilesTests : IDisposable
{
    private static readonly System.Text.Json.JsonSerializerOptions _json = AwJson.CreateOptions(SindriJsonContext.Default);
    private static readonly DateTimeOffset _at = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string LaunchDirectory => Path.Combine(_home.Path, "launch");   // the launcher's directory, where the counters are written

    private DiskDenialFiles Files() => new(LaunchDirectory, _json);

    private void Launch(string launch, DenialFile? counter)
    {
        if (counter is not null)
        {
            var file = LaunchPaths.DenialsFile(LaunchDirectory, LaunchId.From(launch));
            _ = Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllBytes(file, AwJson.Serialize(counter, _json));
        }
    }

    private static DenialFile Counter(string launch, DateTimeOffset updated, string host) =>
        new(LaunchId.From(launch), updated, [new DenialEntry(DenialKind.Network, host, DenialRules.NoGroup, 3, _at, updated)], []);

    [Fact, Trait("Lane", "integration")]
    public async Task Every_launch_with_a_counter_is_listed_newest_first()
    {
        Launch(DenialsRendererTests.LaunchOne, Counter(DenialsRendererTests.LaunchOne, _at, "one.example"));
        Launch(DenialsRendererTests.LaunchTwo, Counter(DenialsRendererTests.LaunchTwo, _at.AddHours(1), "two.example"));

        var files = await Files().List(launch: null, Token);

        Assert.Equal([DenialsRendererTests.LaunchTwo, DenialsRendererTests.LaunchOne], files.Select(static f => f.LaunchId.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task One_launch_can_be_asked_for()
    {
        Launch(DenialsRendererTests.LaunchOne, Counter(DenialsRendererTests.LaunchOne, _at, "one.example"));
        Launch(DenialsRendererTests.LaunchTwo, Counter(DenialsRendererTests.LaunchTwo, _at, "two.example"));

        var files = await Files().List(LaunchId.From(DenialsRendererTests.LaunchTwo), Token);

        Assert.Equal("two.example", Assert.Single(Assert.Single(files).Entries).Target);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_broken_counter_is_skipped()
    {
        var broken = LaunchPaths.DenialsFile(LaunchDirectory, LaunchId.From(DenialsRendererTests.LaunchTwo));
        _ = Directory.CreateDirectory(Path.GetDirectoryName(broken)!);
        await File.WriteAllTextAsync(broken, "{ not json", Token);

        var files = await Files().List(launch: null, Token);

        Assert.Empty(files);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task No_launches_directory_is_an_empty_list()
    {
        var files = await Files().List(launch: null, Token);

        Assert.Empty(files);
    }
}
