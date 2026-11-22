using Aw.Transcripts.Features.ListSessions;
using Aw.Transcripts.Testing;

namespace Aw.Transcripts.Tests.ListSessions;

public sealed class ListSessionsTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task ListSessions_lists_sessions_written_since_the_given_time()
    {
        var service = new ListSessionsService();
        var request = A.ListSessionsRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert lists_sessions_written_since_the_given_time");
    }
}
