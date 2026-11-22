using Aw.Kernel.Services;
using Aw.Transcripts.Core;
using Aw.Transcripts.Core.Refs;
using Aw.Transcripts.Features.ListSessions;
using Aw.Transcripts.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Transcripts.Tests.ListSessions;

public sealed class ListSessionsTests
{
    private static readonly InMemoryTranscriptCorpus _corpus = new(StandardCorpus.Transcripts(name => TranscriptRefs.ForFile(TranscriptProvider.ClaudeCode, $"fake/{name}.jsonl")));

    [Fact, Trait("Lane", "unit")]
    public async Task ListSessions_lists_sessions_written_since_the_given_time()
    {
        var service = new ListSessionsService(_corpus);
        var request = A.ListSessionsRequest().Since(StandardCorpus.Start.AddMinutes(25)).Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.Equal([StandardCorpus.Start.AddHours(1), StandardCorpus.Start.AddMinutes(30)], result.Value.Sessions.Select(s => s.LastWrittenAt));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ListSessions_refuses_a_relative_repo()
    {
        var services = new ServiceCollection().AddSingleton<Core.Ports.ITranscriptCorpus>(_corpus);
        _ = Features.TranscriptsFeaturesServiceCollectionExtensions.AddTranscriptsFeatures(services);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IService<ListSessionsRequest, ListSessionsResponse>>();

        var result = await service.Handle(A.ListSessionsRequest().InRepo("work/alpha").Build(), CancellationToken.None);

        Assert.Equal(TranscriptsErrors.Invalid.Code, result.Error.Code);
    }
}
