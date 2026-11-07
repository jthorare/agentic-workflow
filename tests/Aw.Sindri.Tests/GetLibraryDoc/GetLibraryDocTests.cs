using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.GetLibraryDoc;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Tests.GetLibraryDoc;

/// <summary>One design doc (PR review page spec §6.1): the service's answer and the adapter's text for a doc's errors and path.</summary>
[Trait("Lane", "unit")]
public sealed class GetLibraryDocTests
{
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

    private sealed class Facts(Result<LibraryDocView?> answer) : ILibraryDocFacts
    {
        public Facts(LibraryDocView? doc)
            : this(Results.Success(doc))
        {
        }

        public ValueTask<Result<LibraryDocView?>> Find(string doc, CancellationToken cancellationToken) => ValueTask.FromResult(answer);
    }
}
