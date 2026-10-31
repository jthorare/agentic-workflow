using System.Net;
using Aw.Kernel;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The contract (<see cref="SindriApiContract"/>, continued): the pasted-pages preview answers, sends the paste and the repo, and a server without the route is an error and not a crash.</summary>
public abstract partial class SindriApiContract
{
    [Fact]
    public async Task ContextPagesPreview_answers_each_block_with_its_match_reason_and_page_to_save()
    {
        var api = Reach(new FakeSindriApi { ContextPagesPreviewAnswer = _ => AConcepts.Served(AConcepts.Previewed()) });

        var view = (await api.ContextPagesPreview(_api, "Schedule week\nSubmit schedule", "aw-split-panes", Token)).Value;

        Expect.Equal(("v41", 2), (view.MapVersion, view.Blocks.Count));
        Expect.True(view.ProfileHash.Length > 0);
        var first = view.Blocks[0];
        Expect.Equal((1, true, "surface:route/GET:/schedule/week", 4), (first.Block, first.Ticked, first.Match!.Key, first.Match.Actions));
        Expect.Equal("action:WeekPage.submit", first.Page!.Actions[0].Action);
        var second = view.Blocks[1];
        Expect.Equal((5, false, "No concept called \"aproval\": pick one"), (second.Line, second.Ticked, second.Reason));
        Expect.Sequence(["aproval"], second.UnknownConcepts);
        Expect.True(second.Page is null && second.Actions[0].Concepts[0].Id is null);
    }

    [Fact]
    public async Task ContextPagesPreview_sends_the_paste_and_the_repo()
    {
        var server = new FakeSindriApi { ContextPagesPreviewAnswer = _ => AConcepts.Served(AConcepts.Previewed()) };
        var api = Reach(server);

        _ = await api.ContextPagesPreview(_api, "Schedule week\nSubmit schedule", "aw-split-panes", Token);
        _ = await api.ContextPagesPreview(_api, "Home", repo: null, Token);

        Expect.Equal(new PagesPreviewBody("Schedule week\nSubmit schedule", "aw-split-panes"), server.ContextPagesPreviewAsked[0]);
        Expect.Equal(new PagesPreviewBody("Home"), server.ContextPagesPreviewAsked[1]);
    }

    [Fact]
    public async Task ContextPagesPreview_hands_back_the_refusal_of_an_empty_paste()
    {
        var server = new FakeSindriApi { ContextPagesPreviewAnswer = _ => RouteAnswer.Problem(HttpStatusCode.BadRequest, "SND-CONTEXT-259", "pages preview: nothing pasted (fix: one page per block: its name, then one action per line)") };

        var result = await Reach(server).ContextPagesPreview(_api, " ", repo: null, Token);

        Expect.Equal(("SND-CONTEXT-259", ErrorKind.Invalid), (result.Error.Code, result.Error.Kind));
    }

    [Fact]
    public async Task ContextPagesPreview_from_a_server_without_the_route_is_an_error_and_not_a_crash()
    {
        var api = Reach(new FakeSindriApi());

        var result = await api.ContextPagesPreview(_api, "Home", repo: null, Token);

        Expect.True(result.IsError);
    }
}
