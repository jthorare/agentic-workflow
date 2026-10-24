using Aw.Judge.Core;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;
using Aw.Judge.Features;
using Aw.Judge.Features.AskQuestion;
using Aw.Judge.Host;
using Aw.Judge.Testing;
using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Aw.Kernel.Services;
using Aw.Kernel.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Judge.Tests.AskQuestion;

/// <summary>The AskQuestion service as the Core and Features registrations build it, over in-memory fakes.</summary>
internal sealed class AskQuestionHarness
{
    public InMemoryDecisionLog Log { get; } = new();

    public InMemoryJudgeConfigStore Config { get; } = new();

    public async Task<Result<AskQuestionResponse>> Ask(AskQuestionRequest request, params IModelProvider[] providers)
    {
        var services = new ServiceCollection()
            .AddSingleton<TimeProvider>(new FakeTimeProvider(KnownInstants.Epoch))
            .AddSingleton(AwJson.CreateOptions(JudgeJsonContext.Default))
            .AddSingleton<IDecisionLog>(Log)
            .AddSingleton<IJudgeConfigStore>(Config)
            .AddSingleton<IBriefStore>(new InMemoryBriefStore())
            .AddSingleton<IPromptSortStore>(new InMemoryPromptSortStore())
            .AddSingleton<ISortSessionStore>(new InMemorySortSessionStore())
            .AddSingleton<IBatchClassifier>(FakeBatchClassifier.Unavailable())
            .AddSingleton<IInputRedactor>(new FakeInputRedactor())
            .AddSingleton<IIdGenerator>(new SequentialIdGenerator())
            .AddSingleton<IProviderCatalog>(new FakeProviderCatalog(providers))
            .AddJudgeCore()
            .AddJudgeFeatures();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IService<AskQuestionRequest, AskQuestionResponse>>();
        return await service.Handle(request, CancellationToken.None);
    }

    public static ScriptedProvider Cli(params ContentClass[] classes) => new(ProviderName.ClaudeCli, classes);
}
