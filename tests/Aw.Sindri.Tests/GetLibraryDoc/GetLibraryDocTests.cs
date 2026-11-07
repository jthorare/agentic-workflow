using Aw.Context.Core.Library;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.GetLibraryDoc;

namespace Aw.Sindri.Tests.GetLibraryDoc;

/// <summary>One design doc (PR review page spec §6.1): the service's answer and the adapter's text for a doc's errors and path.</summary>
[Trait("Lane", "unit")]
public sealed class GetLibraryDocTests
{
    private const string Key = "/home/thor/.agentic-workflow/acme-api/plans/booking-retry-tdd.md";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_known_id_answers_the_doc()
    {
        var doc = new LibraryDocView("booking-retry-tdd", "aw", "aw/acme-api", "tdd", "draft", Project: null, [], [], "Booking retry TDD", "~/x.md", []);
        var status = new ProjectorProgress(TimeProvider.System);

        var answer = await new GetLibraryDocService(new Facts(doc), status).Handle(new GetLibraryDocRequest("booking-retry-tdd"), Ct);

        Assert.Equal(doc, answer.Value.Doc);
    }

    [Fact]
    public async Task An_unknown_id_is_SND_LIB_404_naming_the_id_and_the_verb_that_lists_the_docs()
    {
        var status = new ProjectorProgress(TimeProvider.System);

        var answer = await new GetLibraryDocService(new Facts(doc: null), status).Handle(new GetLibraryDocRequest("nope"), Ct);

        Assert.True(answer.IsError);
        Assert.Equal("SND-LIB-404", answer.Error.Code);
        Assert.Equal("No design doc 'nope'; `sindri library list` names them", answer.Error.Message);
    }

    [Fact]
    public async Task A_library_that_cannot_be_read_is_passed_on()
    {
        var status = new ProjectorProgress(TimeProvider.System);

        var answer = await new GetLibraryDocService(new Facts(Results.Failure<LibraryDocView?>(SindriErrors.ServiceFailed.With("store"))), status).Handle(new GetLibraryDocRequest("x"), Ct);

        Assert.Equal(SindriErrors.ServiceFailed.Code, answer.Error.Code);
    }

    [Theory]
    [InlineData("/home/thor/a.md", "/home/thor", "~/a.md")]
    [InlineData("/home/thor/a.md", "/home/thor/", "~/a.md")]
    [InlineData("/home/thorn/a.md", "/home/thor", "/home/thorn/a.md")]
    [InlineData("/srv/a.md", "", "/srv/a.md")]
    public void The_home_folder_is_written_as_a_tilde_only_at_a_folder_boundary(string path, string home, string shown) =>
        Assert.Equal(shown, ContextLibraryDocFacts.Fold(path, home));

    [Fact]
    public void An_error_reads_as_path_line_message_then_the_fix()
    {
        var error = new DesignDocError("SND-LIB-010", 4, Key + ":4: status 'draft2' isn't allowed.", "Use draft.");

        var text = ContextLibraryDocFacts.Text(error, Key, "~/.agentic-workflow/acme-api/plans/booking-retry-tdd.md");

        Assert.Equal("~/.agentic-workflow/acme-api/plans/booking-retry-tdd.md line 4: status 'draft2' isn't allowed. Use draft.", text);
    }

    [Fact]
    public void A_whole_file_error_keeps_its_message_with_the_path_shown_and_no_fix_adds_nothing()
    {
        var error = new DesignDocError("SND-LIB-018", Line: null, Key + ": too large: the file is over 1 MiB.", "");

        Assert.Equal("~/p.md: too large: the file is over 1 MiB.", ContextLibraryDocFacts.Text(error, Key, "~/p.md"));
    }

    private sealed class Facts(Result<LibraryDocView?> answer) : ILibraryDocFacts
    {
        public Facts(LibraryDocView? doc)
            : this(Results.Success(doc))
        {
        }

        public ValueTask<Result<LibraryDocView?>> Find(string id, CancellationToken cancellationToken) => ValueTask.FromResult(answer);
    }
}
