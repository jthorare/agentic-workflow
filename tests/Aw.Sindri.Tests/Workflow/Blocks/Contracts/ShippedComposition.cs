using System.Text.Json;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The real host composition and hash options the block contracts bind to.</summary>
internal static class ShippedComposition
{
    private static readonly string _stateRoot = Path.Combine(Path.GetTempPath(), "aw-sindri-blocks-test");

    // why: tests that only read the catalog or step runtimes (stateless singletons) shared one composition per run instead of ~20 ms each (#1546);
    // a test that creates a scope or runs a step composes its own. Lives for the test process.
    private static readonly Lazy<ServiceProvider> _shared = new(static () => Compose());

    /// <summary>One composition for tests that only read singletons from it; never dispose it, never create a scope from it.</summary>
    public static ServiceProvider Shared => _shared.Value;

    public static JsonSerializerOptions Json => SliceHarness.Json;

    public static ServiceProvider Compose() => Compose(_stateRoot, proveBlocks: null);

    /// <summary>The composition over <paramref name="stateRoot"/>, with AW_SINDRI_PROVE_BLOCKS set to <paramref name="proveBlocks"/>.</summary>
    public static ServiceProvider Compose(string stateRoot, string? proveBlocks) =>
        Composition.Compose(new ServiceCollection(), name => name switch { "AW_STATE_DIR" => stateRoot, "AW_SINDRI_PROVE_BLOCKS" => proveBlocks, _ => null }, new FakeTimeProvider())
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

    public static EffectContext Effect(string id) => new(
        BlockId.From(id), "WFI-a:1:0", "c-1", FromEngine: false, "T-1/recipe/1", "T-1", Attempt: 1,
        new Dictionary<string, JsonElement>(StringComparer.Ordinal), new Dictionary<FactKey, JsonElement>(), Text: null);
}
