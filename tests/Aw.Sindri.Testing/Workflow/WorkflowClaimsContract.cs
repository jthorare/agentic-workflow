using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Xunit;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>
/// The contract every <see cref="IWorkflowClaims"/> honours (engine spec §5.1, §5.7, §12 'workflow_claims'): a free
/// item is taken, its holder keeps it against any other instance, taking it again is harmless, and only the holder the
/// caller found ended is replaced.
/// </summary>
public abstract class WorkflowClaimsContract
{
    private static readonly ItemKey _item = ItemKey.From("T-3");
    private static readonly WorkflowClaim _first = new(_item, InstanceAddress.From("T-3/item/1"), 1);
    private static readonly WorkflowClaim _second = new(_item, InstanceAddress.From("T-3/item/2"), 2);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A fresh store with no claims.</summary>
    /// <returns>The store.</returns>
    protected abstract IWorkflowClaims Create();

    /// <summary>An item never claimed has no claim, and the first take holds it.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_free_item_is_taken_and_found()
    {
        var claims = Create();

        var before = await claims.Find(_item, Token);
        var taken = await claims.Take(_first, ended: null, Token);
        var after = await claims.Find(_item, Token);

        Expect.True(before.Value is null);
        Expect.Equal(_first, taken.Value);
        Expect.Equal(_first, after.Value);
    }

    /// <summary>Taking the claim the same owner holds changes nothing.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Taking_it_again_is_harmless()
    {
        var claims = Create();
        _ = await claims.Take(_first, ended: null, Token);

        var again = await claims.Take(_first, ended: null, Token);

        Expect.Equal(_first, again.Value);
        Expect.Equal(_first, (await claims.Find(_item, Token)).Value);
    }

    /// <summary>Another instance gets the holder back and takes nothing.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Another_instance_gets_the_holder()
    {
        var claims = Create();
        _ = await claims.Take(_first, ended: null, Token);

        var refused = await claims.Take(_second, ended: null, Token);

        Expect.Equal(_first, refused.Value);
        Expect.Equal(_first, (await claims.Find(_item, Token)).Value);
    }

    /// <summary>The holder the caller found ended is replaced.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_ended_holder_is_replaced()
    {
        var claims = Create();
        _ = await claims.Take(_first, ended: null, Token);

        var taken = await claims.Take(_second, _first.Owner, Token);

        Expect.Equal(_second, taken.Value);
        Expect.Equal(_second, (await claims.Find(_item, Token)).Value);
    }

    /// <summary>Naming an ended instance that isn't the holder replaces nothing.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Naming_another_ended_instance_replaces_nothing()
    {
        var claims = Create();
        _ = await claims.Take(_first, ended: null, Token);

        var refused = await claims.Take(_second, InstanceAddress.From("T-3/step/1"), Token);

        Expect.Equal(_first, refused.Value);
    }

    /// <summary>Items are claimed independently.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Items_are_claimed_independently()
    {
        var claims = Create();
        var other = new WorkflowClaim(ItemKey.From("T-4"), InstanceAddress.From("T-4/item/1"), 1);
        _ = await claims.Take(_first, ended: null, Token);

        var taken = await claims.Take(other, ended: null, Token);

        Expect.Equal(other, taken.Value);
        Expect.Equal(_first, (await claims.Find(_item, Token)).Value);
    }
}
