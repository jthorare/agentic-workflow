using System.Text.Json;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Tests.Workflow.Wiring;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>HumanTrigger (PB spec W23) is written only when true: false and null serialize as a descriptor without it, so no hash moves.</summary>
public sealed class BlockDescriptorJsonTests
{
    private static readonly BlockDescriptor _effect = WiringCatalog.WithTestBlocks.Find(Core.Workflow.Ids.BlockId.From("effect.test.merge"))! with { HumanTrigger = null };

    [Fact, Trait("Lane", "unit")]
    public void HumanTrigger_false_serializes_exactly_as_null()
    {
        var unset = JsonSerializer.Serialize(_effect, SliceHarness.Json);

        Assert.DoesNotContain("humanTrigger", unset, StringComparison.Ordinal);
        Assert.Equal(unset, JsonSerializer.Serialize(_effect with { HumanTrigger = false }, SliceHarness.Json));
        Assert.Equal(PinRules.Hash(_effect, SliceHarness.Json), PinRules.Hash(_effect with { HumanTrigger = false }, SliceHarness.Json));
    }

    [Fact, Trait("Lane", "unit")]
    public void HumanTrigger_true_is_written_last()
    {
        var json = JsonSerializer.Serialize(_effect with { HumanTrigger = true }, SliceHarness.Json);

        Assert.EndsWith("\"humanTrigger\":true}", json, StringComparison.Ordinal);
    }
}
