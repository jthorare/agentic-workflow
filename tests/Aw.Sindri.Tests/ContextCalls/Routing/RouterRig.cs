using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Models;
using Aw.Judge.Core.Providers;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Host.Models;
using Aw.Sindri.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.ContextCalls.Routing;

/// <summary>A router over scripted CLIs, with a job context to run calls under.</summary>
internal sealed class RouterRig
{
    private readonly FakeJobContextAccessor _jobs = new();

    public RouterRig(ModelsSettings? settings, params ScriptedCli[] clis)
    {
        Catalog = new ScriptedCatalog(clis);
        Clis = clis;
        Port = new RoutedModelPort(() => Catalog, new FixedModels(settings ?? new ModelsSettings()), _jobs, Calls, Clock);
    }

    public ScriptedCatalog Catalog { get; }

    public ScriptedCli[] Clis { get; }

    public RecordedCalls Calls { get; } = new();

    public FakeTimeProvider Clock { get; } = new();

    public FakeJobWaiting Waiting { get; } = new();

    public LineProgress Progress { get; } = new();

    public RoutedModelPort Port { get; }

    public static ModelRequest Request(ModelPurpose purpose = ModelPurpose.Labeling) => new(purpose, "c1: src/a.ts, src/b.ts", 40, TimeSpan.FromSeconds(30));

    public static ModelsSettings Settings(string json)
    {
        using var document = JsonDocument.Parse(json);
        return ModelsSettings.Read(document.RootElement.Clone()).Value;
    }

    public static ProviderDecided Answered(string text, ProviderUsage? usage = null) =>
        new("answered", 0.9, "ok") { Extra = new JsonObject { ["text"] = text }, Usage = usage };

    public static ProviderErrored Limit() => new("cli-reported-error") { UsageLimit = true, LimitPattern = "claude-limit" };

    public IDisposable EnterJob(string id = "a", bool parkable = true) =>
        _jobs.Enter(new JobContext(JobId.From("JOB-" + new string(id[0], 32)), JobKind.From("BuildContextMap"), Progress, parkable ? Waiting : null));

    public ValueTask<Result<ModelAnswer>> Ask(ModelRequest request) => Port.Ask(request, TestContext.Current.CancellationToken);
}
