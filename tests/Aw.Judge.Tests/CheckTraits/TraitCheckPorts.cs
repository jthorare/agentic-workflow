using Aw.Judge.Core.Ports;
using Aw.Judge.Infrastructure.Traits;
using Aw.Judge.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Tests.CheckTraits;

/// <summary>The ports trait-check's service needs, for a harness that builds the whole Features registration over fakes but never runs it.</summary>
internal static class TraitCheckPorts
{
    internal static IServiceCollection AddTraitCheckFakes(this IServiceCollection services) => services
        .AddSingleton<IProcessRunner>(new FakeProcessRunner())
        .AddSingleton<ITraitStore>(new FileTraitStore(Path.Combine(Path.GetTempPath(), "aw-judge-no-traits")))
        .AddSingleton<ITraitFiles>(new FileTraitFiles(Environment.CurrentDirectory))
        .AddSingleton<ITraitJudge>(new JevTraitJudge(FakeBatchClassifier.Unavailable(), new FakeInputRedactor(), new InMemoryJudgeConfigStore()))
        .AddSingleton<ITraitCheckLog>(new RecordingTraitLog());
}
