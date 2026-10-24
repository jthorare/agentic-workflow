using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Item;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Item;

/// <summary>Create item (UI spec §4.5, §7: `C` opens a small form, title, repo and body → CreateItem, desk), through the catalog's control.</summary>
[Lane(TestLane.Unit)]
public sealed class CreateItemViewModelTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<CreateItemViewModel> Form()
    {
        var catalog = new CatalogViewModel(new FakeClipboard(), _clock);
        catalog.Show(_api, _server, (await _server.Commands(_api, Token)).Value);
        var form = new CreateItemViewModel(catalog, () => _api, _server);
        form.Open();
        return form;
    }

    [Fact]
    public async Task The_form_opens_empty_and_needs_a_title_to_send()
    {
        var form = await Form();

        Assert.Equal((true, string.Empty, false), (form.IsOpen, form.Title, form.CanSubmit));
        form.Title = "Proof: appears live";
        Assert.True(form.CanSubmit);
    }

    [Fact]
    public async Task Submit_sends_the_form_once_and_closes_when_the_outcome_settles()
    {
        var form = await Form();
        (form.Title, form.Repo, form.Body) = ("Proof: appears live", "agentic-workflow", " ");

        await form.Submit(Token);

        Assert.Equal([new CreateItemRequest("Proof: appears live", "agentic-workflow", Body: null, Link: null)], _server.Created);
        Assert.Equal(("Create item: done", "ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a306", true), (form.Control!.Outcome, form.CreatedId, form.IsOpen));

        _clock.Advance(ControlViewModel.AcceptedFor);

        Assert.Equal((false, string.Empty, string.Empty), (form.IsOpen, form.Title, form.Repo));
    }

    // §4.1 Error: a refusal is the API's code and detail inline; the form keeps its text.
    [Fact]
    public async Task A_refused_create_keeps_the_form_and_shows_the_apis_detail()
    {
        _server.Create = RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-API-009", "Sindri is busy with another command (fix: retry)");
        var form = await Form();
        form.Title = "Kept";

        await form.Submit(Token);

        Assert.Equal(("SND-API-009", "Sindri is busy with another command (fix: retry)"), (form.Control!.ErrorCode, form.Control.ErrorDetail));
        Assert.Equal((true, "Kept"), (form.IsOpen, form.Title));
    }

    // §7: while the catalog says pending:P3 (it did before P3 shipped), the form's button is drawn disabled with its phase and CLI.
    // L10 (corr-5): the second press, and one in the 2 s the accepted outcome shows, send nothing more: one create.
    [Fact]
    public async Task Submit_twice_while_sending_and_again_while_the_outcome_shows_creates_once()
    {
        var form = await Form();
        form.Title = "Once";
        var gate = _server.CommandGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            var first = form.Submit(Token);
            var second = form.Submit(Token);
            gate.SetResult();
            await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, Token);
        }
        finally
        {
            _ = gate.TrySetResult();
        }

        await form.Submit(Token).WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, Token);

        _ = Assert.Single(_server.Created);
        Assert.Equal((false, true), (form.CanSubmit, form.IsOpen));
    }

    // L10 (a11y-5): Create stays enabled; a blank submit says what is missing under the field, speaks it once (the flag
    // before the text, never an empty text), asks for focus on the title and sends nothing. Typing a title takes the message away.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_submit_says_the_title_is_required_once_and_sends_nothing(string title)
    {
        var form = await Form();
        form.Title = title;
        var spoken = new List<(bool Live, string Text)>();
        form.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is { } name && (string.Equals(name, nameof(CreateItemViewModel.IsAnnouncing), StringComparison.Ordinal) || string.Equals(name, nameof(CreateItemViewModel.Announcement), StringComparison.Ordinal)))
            {
                spoken.Add((form.IsAnnouncing, form.Announcement));
            }
        };

        await form.Submit(Token);

        Assert.Empty(_server.Created);
        Assert.Equal((true, "Title is required", 1, CommandPhase.Idle), (form.HasTitleError, form.TitleError, form.TitleFocusRequests, form.Control!.Phase));
        // The region goes live (on its resting text, unspoken) before the text changes, and the text is never empty.
        Assert.Equal([(true, "Title (required)"), (true, "Title is required")], spoken);
        Assert.All(spoken, s => Assert.NotEmpty(s.Text));

        form.Title = "Now there is one";

        Assert.Equal((false, string.Empty), (form.HasTitleError, form.TitleError));
    }

    // corr-2: an answer belongs to the form that sent it; Esc and C make a new form, and the old answer does not close its door.
    [Fact]
    public async Task A_late_answer_for_a_closed_form_does_not_block_the_form_opened_since()
    {
        var form = await Form();
        form.Title = "First";
        var gate = _server.CommandGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var sent = form.Submit(Token);
            form.Close();
            form.Open();

            gate.SetResult();
            await sent.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, Token);
        }
        finally
        {
            _ = gate.TrySetResult();
        }

        Assert.Equal(string.Empty, form.CreatedId);
        form.Title = "Second";
        await form.Submit(Token).WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, Token);

        Assert.Equal(["First", "Second"], _server.Created.Select(c => c.Title), StringComparer.Ordinal);
    }
}
