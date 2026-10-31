using Aw.Context.Core;
using Aw.Context.Core.Pages;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The pages read: the request becomes the engine's query under the approved profile's scope, and the engine's answer comes back as it is.</summary>
public sealed class GetContextPagesServiceTests
{
    private readonly SliceHarness _h = new();
    private readonly PagesEngineProxy _engine = PagesEngineProxy.Create();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<GetContextPagesService> Service()
    {
        _ = await _h.Profiles.Save(SliceHarness.Profile(), Ct);
        return new GetContextPagesService(new ContextScopes(_h.Profiles, _h.Callers), _engine.Proxy);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Pages_maps_the_request_to_the_engine_query_and_passes_the_view_through()
    {
        _engine.Answer = PagesEngineProxy.OneOwnerPage();

        var answer = await (await Service()).Handle(new GetContextPagesRequest("app", "concept:scheduling", "schedule-week", PageMarks.Suggested, 20, 40, Draft: false, MapVersion: "v3"), Ct);

        Assert.Same(_engine.Answer.Value, answer.Value);
        var query = Assert.Single(_engine.Queries);
        Assert.Equal(("scheduling", "schedule-week", "app", PageMarks.Suggested, 20, 40, false, "v3"), (query.Concept?.Value, query.Page, query.Repo?.Value, query.Marks, query.Limit, query.Offset, query.Draft, query.MapVersion));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Pages_asks_for_both_marks_unless_the_caller_narrows_them_and_takes_a_bare_concept_id()
    {
        var service = await Service();

        _ = await service.Handle(new GetContextPagesRequest(Concept: "scheduling"), Ct);

        var query = Assert.Single(_engine.Queries);
        Assert.Equal((PageMarks.All, "scheduling", null, null), (query.Marks, query.Concept?.Value, query.Limit, query.Offset));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Pages_hands_the_engine_s_unknown_page_error_back()
    {
        _engine.Answer = ContextErrors.PageNotFound.With("no page is named 'nope' (fix: try schedule-week)");

        var answer = await (await Service()).Handle(new GetContextPagesRequest(Page: "nope"), Ct);

        Assert.Equal("SND-CONTEXT-257", answer.Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("Not A Concept!", null)]
    [InlineData(null, "No Repo!")]
    public async Task Pages_names_a_concept_or_repo_it_cannot_read_as_258_without_asking_the_engine(string? concept, string? repo)
    {
        var answer = await (await Service()).Handle(new GetContextPagesRequest(repo, concept), Ct);

        Assert.Equal("SND-CONTEXT-258", answer.Error.Code);
        Assert.Empty(_engine.Queries);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Pages_refuses_the_draft_and_a_map_version_together_without_asking_the_engine()
    {
        var answer = await (await Service()).Handle(new GetContextPagesRequest(Draft: true, MapVersion: "v3"), Ct);

        Assert.True(answer.IsError);
        Assert.Empty(_engine.Queries);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Pages_without_an_approved_profile_is_refused()
    {
        var service = new GetContextPagesService(new ContextScopes(_h.Profiles, _h.Callers), _engine.Proxy);

        var answer = await service.Handle(new GetContextPagesRequest(), Ct);

        Assert.Equal("SND-PROFILE-005", answer.Error.Code);
    }
}
