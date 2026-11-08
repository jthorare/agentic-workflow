using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Scoping;
using Xunit;

namespace Aw.Sindri.Testing.Scoping;

/// <summary>The contract every <see cref="IScopeRunProposals"/> honours, run against the in-memory proposals and the context engine's adapter.</summary>
public abstract class ScopeRunProposalsContract
{
    private static readonly RepoName _fallback = RepoName.From("fixture-notes");
    private static readonly InstanceAddress _run = InstanceAddress.From("T-1/scoping/1");

    /// <summary>The repo whose map the run writes to.</summary>
    protected virtual RepoName Repo => _fallback;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The proposals over a repo whose map the run can write to.</summary>
    /// <returns>The proposals.</returns>
    protected abstract IScopeRunProposals Create();

    /// <summary>Each submitted surface is answered once, in order, by its title.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Each_submitted_surface_is_answered_in_order_by_its_title()
    {
        var answer = await Create().Replace(_run, Repo, [new ProposedScopeSurface("Expiry choice", "surface"), new ProposedScopeSurface("Expired page", Kind: null)], Token);

        Expect.True(answer.IsSuccess);
        Expect.Sequence(["Expiry choice", "Expired page"], answer.Value.Select(static s => s.Surface));
        Expect.True(answer.Value.All(static s => new[] { "mapped", "new", "unmapped" }.Contains(s.Status, StringComparer.Ordinal)));
    }

    /// <summary>A withdrawal, no surfaces, answers none.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_withdrawal_answers_no_surfaces()
    {
        var answer = await Create().Replace(_run, Repo, [], Token);

        Expect.True(answer.IsSuccess);
        Expect.Equal(0, answer.Value.Count);
    }
}
