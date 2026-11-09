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
    private readonly InMemoryProfiles _profiles = LinearConnectionTestSupport.Profiles();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private RemoveLinearWriteKeyService Service() => new(_store, _profiles, _slice.Callers, new FakeLinearCheckStore());

    [Fact]
    public async Task A_session_is_refused_SND_AUTH_002_and_nothing_is_removed()
    {
        using var entered = _slice.Callers.Enter(A.Caller().Build());

        var result = await Service().Handle(new RemoveLinearWriteKeyRequest(), Token);

        Expect.Equal(SindriErrors.AuthRefused.Code, result.Error.Code);
        Expect.Equal(0, _store.Removals);
    }

    [Fact]
    public async Task The_pointer_is_removed()
    {
        using var entered = _slice.Callers.Enter(A.Caller().Human().Build());

        var result = await Service().Handle(new RemoveLinearWriteKeyRequest(), Token);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Removed);
        Expect.Equal(_profiles.Live.Value.Hash.Value, result.Value.ProfileHash);
        Expect.Equal(1, _store.Removals);
    }

    [Fact]
    public async Task A_keychain_that_refuses_the_delete_is_that_error()
    {
        using var entered = _slice.Callers.Enter(A.Caller().Human().Build());
        _ = _store.Failing(SindriErrors.LinearNoKeychain.With("Couldn't remove the write key from your keychain"));

        var result = await Service().Handle(new RemoveLinearWriteKeyRequest(), Token);

        Expect.Equal(SindriErrors.LinearNoKeychain.Code, result.Error.Code);
        Expect.Equal(0, _store.Removals);
    }
}
