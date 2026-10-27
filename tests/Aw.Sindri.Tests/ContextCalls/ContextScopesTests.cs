using System.Text.Json;
using Aw.Context.Core.Engine;
using Aw.Context.Testing.Engine;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
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
        profile = profile with { Services = named ? new Dictionary<string, Core.Profile.ServiceSettings>(StringComparer.Ordinal) { ["context-sync"] = new("15m") } : null };

        var scope = ContextScopes.For(profile, ContextScopes.Caller(caller: null));

        Assert.Equal(named, scope.SyncConfigured);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Validate_reports_an_invalid_context_section_with_its_key_path()
    {
        var live = SliceHarness.Profile("5s");
        _h.Profiles.Live = live with { Profile = live.Profile with { Context = JsonDocument.Parse("""{"deadline":"soon"}""").RootElement.Clone() } };
        var service = new ValidateProfileService(_h.Profiles, _h.Profiles, _h.Store, _h.Events, FakeContextEngine.Instance);

        var validated = await service.Handle(new ValidateProfileRequest(), TestContext.Current.CancellationToken);

        Assert.Equal("SND-CONTEXT-001", validated.Error.Code);
        Assert.StartsWith("context.deadline:", validated.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Approve_refuses_a_context_section_naming_a_repo_the_profile_lacks()
    {
        var live = SliceHarness.Profile("5s");
        _h.Profiles.Live = live with { Profile = live.Profile with { Context = JsonDocument.Parse("""{"repos":["nope"]}""").RootElement.Clone() } };
        var service = new Features.ApproveProfile.ApproveProfileService(_h.Profiles, _h.Profiles, _h.Held, _h.Store, _h.Events, new Features.ApproveProfile.ProfileChecks(FakeContextEngine.Instance, new Testing.FixedAgentCatalog()));

        var approved = await service.Handle(new Features.ApproveProfile.ApproveProfileRequest(Hash: null, Answer: null), TestContext.Current.CancellationToken);

        Assert.Equal("SND-CONTEXT-001", approved.Error.Code);
        Assert.StartsWith("context.repos: 'nope' is not a profile repo", approved.Error.Message, StringComparison.Ordinal);
    }
}
