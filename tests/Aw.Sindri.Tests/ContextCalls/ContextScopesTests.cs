using System.Text.Json;
using Aw.Context.Core.Engine;
using Aw.Context.Testing.Engine;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Features.ValidateProfile;

namespace Aw.Sindri.Tests.ContextCalls;

public sealed class ContextScopesTests
{
    private readonly SliceHarness _h = new();

    [Theory, Trait("Lane", "unit")]
    [InlineData(CallerClass.Human, ClientLabel.Cli, ContextCallerKind.Human, ContextClient.Cli)]
    [InlineData(CallerClass.Human, ClientLabel.Ui, ContextCallerKind.Human, ContextClient.Http)]
    [InlineData(CallerClass.Session, ClientLabel.Mcp, ContextCallerKind.Session, ContextClient.Mcp)]
    [InlineData(CallerClass.Session, ClientLabel.Cli, ContextCallerKind.Session, ContextClient.Cli)]
    [InlineData(CallerClass.Human, ClientLabel.Prover, ContextCallerKind.Human, ContextClient.Http)]
    [InlineData(CallerClass.Human, ClientLabel.None, ContextCallerKind.Human, ContextClient.Http)]
    [InlineData(CallerClass.Session, ClientLabel.None, ContextCallerKind.Session, ContextClient.Http)]
    public void A_sindri_caller_maps_to_the_engine_s_caller_and_transport(CallerClass caller, ClientLabel client, ContextCallerKind kind, ContextClient transport)
    {
        var context = CallerContext.Http(new HttpCaller(caller, Terminal: true, client, RepoName.From("split-panes")), new SequentialIdGenerator());

        var mapped = ContextScopes.Caller(context);

        Assert.Equal((kind, transport), (mapped.Kind, mapped.Client));
        Assert.Equal(caller == CallerClass.Session ? "split-panes" : null, mapped.Repo?.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void No_caller_or_a_service_is_the_engine_s_service() =>
        Assert.Equal(ContextCallerKind.Service, ContextScopes.Caller(caller: null).Kind);

    [Theory, Trait("Lane", "unit")]
    [InlineData(true)]
    [InlineData(false)]
    public void The_scope_says_whether_the_profile_names_context_sync(bool named)
    {
        var profile = SliceHarness.Profile("5s").Profile;
        profile = profile with { Services = named ? new Dictionary<string, ServiceSettings>(StringComparer.Ordinal) { ["context-sync"] = new("15m") } : null };

        var scope = ContextScopes.For(profile, ContextScopes.Caller(caller: null));

        Assert.Equal(named, scope.SyncConfigured);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Validate_reports_an_invalid_context_section_with_its_key_path()
    {
        var live = SliceHarness.Profile("5s");
        _h.Profiles.Live = live with { Profile = live.Profile with { Context = JsonDocument.Parse("""{"deadline":"soon"}""").RootElement.Clone() } };
        var service = new ValidateProfileService(_h.Profiles, FakeContextEngine.Instance);

        var validated = await service.Handle(new ValidateProfileRequest(), TestContext.Current.CancellationToken);

        Assert.Equal("SND-CONTEXT-001", validated.Error.Code);
        Assert.StartsWith("context.deadline:", validated.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_profile_repo_that_is_not_a_git_checkout_is_left_out_of_the_scope_and_named()
    {
        WithRepos(("aw", "/r/aw"), ("notes", "/r/notes"), ("docs", "/r/docs"));
        var scopes = new ContextScopes(_h.Profiles, _h.Callers, new Checkouts("/r/aw", "/r/docs"));

        var scope = (await scopes.Current(TestContext.Current.CancellationToken)).Value;

        Assert.Equal(["aw", "docs"], scope.Repos.Select(r => r.Name.Value), StringComparer.Ordinal);
        Assert.Equal(["notes"], scope.LeftOut.Select(r => r.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Every_repo_a_checkout_leaves_nothing_out()
    {
        WithRepos(("aw", "/r/aw"), ("docs", "/r/docs"));
        var scopes = new ContextScopes(_h.Profiles, _h.Callers, new Checkouts("/r/aw", "/r/docs"));

        var scope = (await scopes.Current(TestContext.Current.CancellationToken)).Value;

        Assert.Equal(["aw", "docs"], scope.Repos.Select(r => r.Name.Value), StringComparer.Ordinal);
        Assert.Empty(scope.LeftOut);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_missing_repo_folder_stays_in_the_scope_so_its_reads_still_fail_loudly()
    {
        WithRepos(("aw", "/r/aw"), ("work", "/gone/work"));
        var scopes = new ContextScopes(_h.Profiles, _h.Callers, new Checkouts("/r/aw"));

        var scope = (await scopes.Current(TestContext.Current.CancellationToken)).Value;

        Assert.Equal(["aw", "work"], scope.Repos.Select(r => r.Name.Value), StringComparer.Ordinal);
        Assert.Empty(scope.LeftOut);
    }

    private void WithRepos(params (string Name, string Path)[] repos)
    {
        var live = SliceHarness.Profile("5s");
        _h.Profiles.Live = live with { Profile = live.Profile with { Repos = [.. repos.Select(r => new RepoEntry(r.Name, r.Path))] } };
    }

    // The checkout port with a fixed answer: the paths given are checkouts, a path under /gone/ is missing, every other path is a plain folder.
    private sealed class Checkouts(params string[] paths) : IRepoCheckouts
    {
        public RepoCheckout Of(string path) =>
            paths.Contains(path, StringComparer.Ordinal) ? RepoCheckout.Checkout : path.StartsWith("/gone/", StringComparison.Ordinal) ? RepoCheckout.Missing : RepoCheckout.PlainFolder;
    }
}
