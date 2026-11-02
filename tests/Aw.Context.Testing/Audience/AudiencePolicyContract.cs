using Aw.Context.Core.Audience;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Records;
using Aw.Context.Testing.Records;
using Xunit;

namespace Aw.Context.Testing.Audience;

/// <summary>
/// The contract every <see cref="IAudiencePolicy"/> honours (spec §4.4): a session sees its own repo's audience and the teams
/// and channels the profile maps to it, nothing else, a record with no audience is seen by no one, and a repo the profile
/// doesn't name sees nothing. A binding creates a policy where <see cref="Repo"/> maps to <see cref="OwnRepo"/> and
/// <see cref="Team"/>, and no repo named <see cref="Unnamed"/> exists.
/// </summary>
/// <typeparam name="TPolicy">The implementation.</typeparam>
public abstract class AudiencePolicyContract<TPolicy>
    where TPolicy : IAudiencePolicy
{
    private static readonly string _owner = typeof(TPolicy).Name;

    /// <summary>The repo the bound policy maps.</summary>
    protected static ContextRepoName Repo { get; } = ContextRepoName.From("split-panes");

    /// <summary>A repo the bound policy does not name.</summary>
    protected static ContextRepoName Unnamed { get; } = ContextRepoName.From("elsewhere");

    /// <summary>The repo's own audience.</summary>
    protected static AudienceKey OwnRepo { get; } = AudienceKey.From("repo:acme/split-panes");

    /// <summary>A Linear team the profile maps to the repo.</summary>
    protected static AudienceKey Team { get; } = AudienceKey.From("linear:team-a");

    /// <summary>A fresh policy as described on the type.</summary>
    /// <returns>The policy.</returns>
    protected abstract TPolicy Create();

    /// <summary>A repo's scope allows its own audience and its mapped team, and nothing else.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_scope_allows_the_repo_and_its_teams_and_nothing_else()
    {
        var scope = ContractCheck.Ok(_owner, await Create().For(Repo, CancellationToken.None));
        ContractCheck.That(_owner, scope.Allows(OwnRepo) && scope.Allows(Team), "the repo's own audience and its team are allowed");
        ContractCheck.That(_owner, !scope.Allows(AudienceKey.From("linear:team-b")) && !scope.Allows(AudienceKey.From("slack:C123")), "another team or a channel is not");
        ContractCheck.That(_owner, !scope.Allows(audience: null), "a record with no audience is visible to no one");
    }

    /// <summary>A repo the profile doesn't name sees nothing.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_repo_the_profile_does_not_name_sees_nothing()
    {
        var scope = ContractCheck.Ok(_owner, await Create().For(Unnamed, CancellationToken.None));
        ContractCheck.That(_owner, scope.Allowed.Count == 0 && !scope.Allows(OwnRepo), "an unnamed repo has an empty scope");
    }
}
