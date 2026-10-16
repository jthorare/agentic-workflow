using System.Text;
using System.Text.Json;
using Aw.Evidence.Core.Data;
using Aw.Evidence.Features.ApiRun;
using Aw.Evidence.Testing;
using Aw.Kernel.Services;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Evidence.Tests.ApiRun;

/// <summary>The api-run slice over in-memory fakes: the script at a fixed path, a loopback host, a fake transport.</summary>
internal sealed class ApiRunHarness
{
    internal const string ScriptPath = "/work/api.json";
    internal const string RunDirectory = "/runs/api-1";
    internal const string Repo = "/work/repo";
    internal const string Origin = "http://localhost:3000";

    internal static string SummaryPath => RunDirectory + "/summary.json";

    internal InMemoryRunFiles Files { get; } = new();

    internal InMemoryMachineLocks Locks { get; } = new();

    internal FakeApiTransport Transport { get; } = new();

    internal FakeCliRunner Cli { get; } = new();

    internal FakeExecutableLocator Locator { get; } = new();

    internal FakeEvidenceTargets Targets { get; } = new();

    internal Dictionary<string, string> Environment { get; } = new(StringComparer.Ordinal);

    internal FakeTextScrubber Scrubber { get; } = new();

    internal FakeEvidenceSettings Settings { get; } = new() { PreviewHostPattern = FakeEvidenceSettings.ExamplePreviewHostPattern };

    internal SequentialIds Ids { get; } = new();

    internal FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 15, 9, 0, 0, TimeSpan.Zero));

    internal ApiRunHarness WithScript(string json)
    {
        _ = Files.With(ScriptPath, Encoding.UTF8.GetBytes(json));
        return this;
    }

    internal static ApiRunRequest Request(string? host = Origin, DataRunAt at = DataRunAt.Head, string? appBuild = null, bool allowPreviewHost = false) =>
        new(ScriptPath, RunDirectory, at, TimeSpan.Zero, Repo, host, appBuild, allowPreviewHost);

    internal ValueTask<Kernel.Result<ApiRunResponse>> Run(ApiRunRequest? request = null)
    {
        var service = new ValidatingService<ApiRunRequest, ApiRunResponse>(
            new ApiRunService(
                new DataRunFiles(Files, Scrubber, TestJson.Options),
                new ApiVetting(Settings, new FakeSecrets(Environment), new DataRunFiles(Files, Scrubber, TestJson.Options), Targets, Locator),
                new RequestRunner(Transport), new CallRunner(Cli), Locks, Clock, Ids), new ApiRunValidator());
        return service.Handle(request ?? Request(), TestContext.Current.CancellationToken);
    }

    /// <summary>The summary document a run produced, parsed.</summary>
    internal static JsonElement WrittenSummaryOf(ApiRunResponse response) => JsonDocument.Parse(response.Document).RootElement;
}
