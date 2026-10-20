using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>
/// No shipped descriptor's hash moves when a descriptor field is added (PB spec W23): a new field is written only when
/// set, so every approval that pins a descriptor still holds. A block added later is not listed; re-pin a listed block
/// only when its behaviour changes on purpose.
/// </summary>
public sealed class ShippedDescriptorHashesTests
{
    private static readonly string _stateRoot = Path.Combine(Path.GetTempPath(), "aw-sindri-descriptor-hashes-" + RealTime.NewGuid().ToString("N"));

    [Fact, Trait("Lane", "unit")]
    public void Every_shipped_descriptor_hash_is_unchanged()
    {
        using var provider = Composition.Compose(new ServiceCollection(), name => name == "AW_STATE_DIR" ? _stateRoot : null, new FakeTimeProvider())
            .BuildServiceProvider();
        var current = provider.GetRequiredService<BlockCatalog>().Blocks
            .Where(b => ShippedDescriptorHashes.Pinned.ContainsKey(b.Id.Value))
            .ToDictionary(b => b.Id.Value, b => PinRules.Hash(b, SliceHarness.Json).Value, StringComparer.Ordinal);

        Assert.Equal(ShippedDescriptorHashes.Pinned.Count, current.Count);
        Assert.Equal(ShippedDescriptorHashes.Pinned.OrderBy(p => p.Key, StringComparer.Ordinal), current.OrderBy(p => p.Key, StringComparer.Ordinal));
    }
}
