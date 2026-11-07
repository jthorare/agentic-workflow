using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Features.LinearConnection;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.LinearConnection;

/// <summary>DELETE /sindri/linear/connection/write: removes the write key's pointer. Owner only.</summary>
[Lane(TestLane.Unit)]
public sealed class RemoveLinearWriteKeyServiceTests
{
    private readonly SliceHarness _slice = new();
    private readonly FakeLinearConnectionStore _store = new();
    private readonly FakeLinearConnectionApproval _approval = new();
    private readonly InMemoryProfiles _profiles = LinearConnectionTestSupport.Profiles();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private RemoveLinearWriteKeyService Service() => new(_store, _approval, _profiles, _slice.Callers);

    [Fact]
    public async Task A_session_is_refused_SND_AUTH_002_and_nothing_is_removed()
    {
        using var entered = _slice.Callers.Enter(A.Caller().Build());

        var result = await Service().Handle(new RemoveLinearWriteKeyRequest(), Token);

        Expect.Equal(SindriErrors.AuthRefused.Code, result.Error.Code);
        Expect.Equal(0, _store.Removals);
    }

    [Fact]
    public async Task The_pointer_is_removed_and_the_profile_approved_when_that_is_all_that_waits()
    {
        using var entered = _slice.Callers.Enter(A.Caller().Human().Build());

        var result = await Service().Handle(new RemoveLinearWriteKeyRequest(), Token);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Removed);
        Assert.True(result.Value.Approved);
        Expect.Equal(_profiles.Live.Value.Hash.Value, result.Value.ProfileHash);
        Expect.Equal(1, _store.Removals);
    }

    [Fact]
    public async Task With_other_changes_waiting_the_pointer_is_removed_but_the_profile_is_not_approved()
    {
        using var entered = _slice.Callers.Enter(A.Caller().Human().Build());
        _ = _approval.Settling(approved: false);

        var result = await Service().Handle(new RemoveLinearWriteKeyRequest(), Token);

        Assert.True(result.Value.Removed);
        Assert.False(result.Value.Approved);
    }
}
