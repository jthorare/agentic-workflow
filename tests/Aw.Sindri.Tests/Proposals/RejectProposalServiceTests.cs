using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Evolve;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.RejectProposal;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Proposals;

/// <summary>Rejecting is the owner's: a session is refused before the store is touched.</summary>
[Lane(TestLane.Unit)]
public sealed class RejectProposalServiceTests
{
    private static readonly ProposalId _id = ProposalId.From("prp_" + new string('1', 32));

    private sealed class CountingStore : IProposalStore
    {
        public int Rejects { get; private set; }

        public ValueTask<Result<ProposalSaved>> Save(ProposalToSave proposal, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<IReadOnlyList<StoredProposal>>> List(ProposalState? state, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<StoredProposal?>> Find(ProposalId id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<StoredProposal>> Reject(ProposalId id, CancellationToken cancellationToken)
        {
            Rejects++;
            var stored = new StoredProposal(id, ProposalSeeds.Original("Run the tests"), ProposalState.Rejected, ProposalTier.Code, Attempts: 0, HeldBy: null, Sessions: [], Origin: "test", KnownInstants.Epoch, KnownInstants.Epoch);
            return ValueTask.FromResult<Result<StoredProposal>>(stored);
        }
    }

    private static CallerContext Caller(CallerClass kind) =>
        new(kind, Terminal: kind == CallerClass.Human, kind == CallerClass.Human ? "owner" : "session:s1", CommandId.From(new Guid(1, 0, 0, [0, 0, 0, 0, 0, 0, 0, 1])), CorrelationId.From(new Guid(2, 0, 0, [0, 0, 0, 0, 0, 0, 0, 2])), Repo: null);

    [Fact]
    public async Task A_session_is_refused_and_the_store_is_not_touched()
    {
        var store = new CountingStore();
        var service = new RejectProposalService(store, new FixedCallerContextAccessor(Caller(CallerClass.Session)));

        var answer = await service.Handle(new RejectProposalRequest(_id), TestContext.Current.CancellationToken);

        Assert.Equal(SindriErrors.AuthRefused.Code, answer.Error.Code);
        Assert.Equal(0, store.Rejects);
    }

    [Fact]
    public async Task The_owner_rejects_it()
    {
        var store = new CountingStore();
        var service = new RejectProposalService(store, new FixedCallerContextAccessor(Caller(CallerClass.Human)));

        var answer = await service.Handle(new RejectProposalRequest(_id), TestContext.Current.CancellationToken);

        Assert.Equal(ProposalState.Rejected, answer.Value.Proposal.State);
        Assert.Equal(1, store.Rejects);
    }
}
