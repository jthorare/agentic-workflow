using Aw.Context.Core.Audience;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;

namespace Aw.Context.Tests.Core;

/// <summary>The 'owner:' audience key (PR review page spec 2026-11-04 §5.4, D17): the owner reads owner-only records, no session ever does.</summary>
[Trait("Lane", "unit")]
public sealed class OwnerAudienceTests
{
    private static readonly ContextRepoName _repo = ContextRepoName.From("split-panes");
    private static readonly AudienceKey _library = AudienceKey.From("owner:library");

    private static ContextSettings Settings() => new()
    {
        GitHub = new GitHubSource { Repos = ["acme/split-panes"] },
        Slack = new SlackSource { Channels = [new SlackChannel("C123", "general")] },
        LinearTeams = ["ENG"],
    };

    [Fact]
    public void A_session_whose_records_hold_an_owner_record_still_cannot_read_it()
    {
        var record = RecordKey.From("doc:aw/slug/plans/a.md");
        var scope = new AudienceScope(new HashSet<AudienceKey> { AudienceKey.From("repo:acme/split-panes") }) { Records = new HashSet<RecordKey> { record } };

        Assert.False(scope.Allows(_library, record));
        Assert.True(scope.Allows(AudienceKey.From("repo:acme/split-panes"), record));
        Assert.True(scope.Allows(AudienceKey.From("slack:C123"), record));   // rule (c) still lets a linked Slack thread through
    }

    [Fact]
    public async Task No_session_scope_built_by_the_policy_holds_an_owner_key()
    {
        var policy = new ProfileAudiencePolicy(Settings(), [_repo]);
        var scope = (await policy.For(_repo, TestContext.Current.CancellationToken)).Value;

        Assert.NotEmpty(scope.Allowed);
        Assert.DoesNotContain(scope.Allowed, static k => k.Value.StartsWith("owner:", StringComparison.Ordinal));
        Assert.False(scope.Allows(_library));

        var session = await RecordMask.Allowed(Settings(), [new ContextRepo(_repo, "/work/split-panes")], ContextCallerKind.Session, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(session.Value, static k => k.Value.StartsWith("owner:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_owner_reads_an_owner_library_record()
    {
        var human = await RecordMask.Allowed(Settings(), [new ContextRepo(_repo, "/work/split-panes")], ContextCallerKind.Human, TestContext.Current.CancellationToken);

        Assert.Contains(_library, human.Value);
        Assert.True(new AudienceScope(human.Value).Allows(_library, RecordKey.From("doc:aw/slug/plans/a.md")));
        Assert.Contains(_library, ProfileAudiencePolicy.HumanExtras(new ContextSettings()));   // whatever the profile says
    }

    [Theory]
    [InlineData("owner:library", true)]
    [InlineData("owner:a", true)]
    [InlineData("owner:", false)]
    [InlineData("owner:Library", false)]
    [InlineData("owner:1x", false)]
    [InlineData("owner:has_underscore", false)]
    public void The_owner_key_has_a_lowercase_name_of_at_most_32_characters(string value, bool valid) =>
        Assert.Equal(valid, AudienceKey.TryFrom(value, out _));

    [Fact]
    public void The_owner_key_name_stops_at_32_characters()
    {
        Assert.True(AudienceKey.TryFrom("owner:a" + new string('b', 31), out _));
        Assert.False(AudienceKey.TryFrom("owner:a" + new string('b', 32), out _));
    }

    [Fact]
    public void A_library_doc_naming_one_synced_repo_gets_that_repos_audience()
    {
        string[] synced = ["acme/split-panes", "acme/other"];

        Assert.Equal("repo:acme/split-panes", ProfileAudiencePolicy.LibraryDoc(["acme/split-panes"], synced).Value);
        Assert.Equal("repo:acme/split-panes", ProfileAudiencePolicy.LibraryDoc(["Acme/Split-Panes"], synced).Value);
    }

    [Fact]
    public void Every_other_library_doc_is_owner_only()
    {
        string[] synced = ["acme/split-panes", "acme/other"];

        Assert.Equal("owner:library", ProfileAudiencePolicy.LibraryDoc([], synced).Value);   // no repos: line
        Assert.Equal("owner:library", ProfileAudiencePolicy.LibraryDoc(["acme/split-panes", "acme/other"], synced).Value);   // two repos: the v1 limit
        Assert.Equal("owner:library", ProfileAudiencePolicy.LibraryDoc(["acme/unsynced"], synced).Value);
        Assert.Equal("owner:library", ProfileAudiencePolicy.LibraryDoc(["split-panes"], synced).Value);   // not owner/name
        Assert.Equal("owner:library", ProfileAudiencePolicy.LibraryDoc(["acme/split-panes"], ["acme/split-panes", "fork/split-panes"]).Value);   // two owners for one name: OwnRepo gives none
    }
}
