using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.GetInstance;
using Aw.Sindri.Testing.Scoping;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// The images proof (images spec 8, PR 3): a fixture recipe whose 'step.judge.choose' names two frames (images: fact:frames), one under the item's checkout and one
/// under $AW_STATE_DIR/ui-evidence, runs through the composed control plane on a Temporal dev server, with the REAL judge program as the Step's child. The judge's Jev
/// is a recording stand-in on loopback (AW_JUDGE_JEV_URL). Needs a built judge: AW_SINDRI_JUDGE names it (`heavy --light dotnet build src/Judge/Aw.Judge.Host`,
/// then the apphost under artifacts/bin/Aw.Judge.Host); skips without it, like JudgeCliChoiceRealJudgeTests.
/// </summary>
[Lane(TestLane.Heavy)]
[Collection(nameof(WallClockAlone))]   // sets AW_STATE_DIR and the Jev variables for the whole process: no other test runs beside it
public sealed class JudgeImagesEndToEndTests(BlockRuntimeDevServer server) : IClassFixture<BlockRuntimeDevServer>, IDisposable
{
    private const string RecipeId = "workflow.t-judge-images";
    private const string Repo = "app";
    private static readonly string[] _variables = ["AW_STATE_DIR", "AW_JUDGE_JEV_URL", "AW_JUDGE_JEV_IMAGES", "TYPESAFE_API_KEY", "AW_JUDGE_PROVIDERS", "PATH"];

    private static readonly string[] _agentClis = ["claude", "codex", "cursor-agent"];

    private readonly WorkflowsHarness _harness = new();
    private readonly Dictionary<string, string?> _saved = _variables.ToDictionary(static v => v, static v => Environment.GetEnvironmentVariable(v), StringComparer.Ordinal);
    private readonly List<string> _dirs = [];

    public void Dispose()
    {
        foreach (var (name, value) in _saved)
        {
            Environment.SetEnvironmentVariable(name, value);
        }

        _harness.Dispose();
        foreach (var dir in _dirs)
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact(Timeout = 240_000)]
    public async Task A_judge_choice_with_two_frames_asks_jev_once_with_both_pictures_and_the_runs_list_counts_them()
    {
        var program = Environment.GetEnvironmentVariable("AW_SINDRI_JUDGE");
        Assert.SkipWhen(string.IsNullOrEmpty(program) || !File.Exists(program), "AW_SINDRI_JUDGE does not name a built judge: run `heavy --light dotnet build src/Judge/Aw.Judge.Host` and set it to the judge apphost under artifacts/bin/Aw.Judge.Host");
        var ct = TestContext.Current.CancellationToken;
        var checkout = Temp();
        var state = Temp();
        var frames = new[] { Frame(Path.Combine(checkout, "shot.png")), Frame(Path.Combine(state, "ui-evidence", "runs", "r1", "home.png")) };
        using var jev = RecordingJev.Start();
        Arrange(jev, checkout, state);
        await using var sindri = _harness.Sindri(_harness.Replacing, name => name == "AW_STATE_DIR" ? state : null);
        await WorkflowsHarness.HoldLease(sindri, ct);
        using var worker = new TemporalWorker(server.Environment.Client, sindri.GetRequiredService<TemporalWorkerOptions>());
        await worker.ExecuteAsync(async () =>
        {
            var start = await WorkflowsHarness.RecipeStart(sindri, RecipeId, "T-E2E1", ct) with
            {
                Inputs = new Dictionary<FactKey, InputFact>
                {
                    [FactKey.From("fact:change")] = new InputFact("change.txt", _harness.Fact("Move the export button")),
                    [FactKey.From("fact:frames")] = new InputFact("frames.json", _harness.Fact(JsonSerializer.Serialize(frames))),
                    [FactKey.From("fact:item")] = new InputFact("item.json", _harness.Fact("""{"id":"ITM-00000000000000000000000000000001","repo":"app"}""")),
                },
            };
            var handle = await WorkflowsHarness.Start(server.Environment.Client, start);
            var view = await WorkflowsHarness.Until(handle, static v => v.State is not ("route" or "starting"));

            Assert.True(string.Equals(view.State, "prove", StringComparison.Ordinal), await Explain(sindri, jev, view.State, ct));
            var asked = Assert.Single(jev.Requests);
            Assert.Equal(("multipart/form-data", 2), (asked.ContentType, asked.ImageParts));

            var runs = (await new StepRunReader(sindri.GetRequiredService<IStepRunSource>(), sindri.GetRequiredService<IApprovedRecipes>()).Read(view, ct)).Value;
            var judged = Assert.Single(runs, static r => string.Equals(r.Step, "route", StringComparison.Ordinal));
            Assert.Equal((2, null), (judged.ImageCount, judged.ImagesDropped));   // the Runs list words it "decided with 2 images" (StepRunsViewModel, StepRunsImagesTests)

            Assert.Equal("jev", ProviderOfDecision());
            await handle.TerminateAsync("test over");
        }, ct);
    }

    // The judge child's environment (it inherits this process's), the checkout the item's repo has, and the approved recipe.
    private void Arrange(RecordingJev jev, string checkout, string state)
    {
        Environment.SetEnvironmentVariable("AW_STATE_DIR", state);
        Environment.SetEnvironmentVariable("AW_JUDGE_JEV_URL", jev.Url);
        Environment.SetEnvironmentVariable("AW_JUDGE_JEV_IMAGES", "1");
        Environment.SetEnvironmentVariable("TYPESAFE_API_KEY", "jev-stub-key");
        Environment.SetEnvironmentVariable("AW_JUDGE_PROVIDERS", value: null);   // the Step names its own list: the default one, Jev first
        Environment.SetEnvironmentVariable("PATH", "/usr/bin:/bin");   // a Jev miss must never reach a real model: no agent CLI is on this path
        AssertNoAgentCli();
        _harness.Replacing = services => services.Replace(ServiceDescriptor.Singleton<IRepoCheckouts>(new InMemoryRepoCheckouts().With(Repo, checkout)));
        _harness.Recipe(RecipeId, Recipe(), _harness.Replacing);
    }

    // Why the instance is not in 'prove': what Jev was asked, why the Step failed (the ledger's step.failed reason), and every decision fact stored.
    private async Task<string> Explain(IServiceProvider sindri, RecordingJev jev, string state, CancellationToken ct)
    {
        var rows = (await sindri.GetRequiredService<ILedgerStore>().ReadAfter(0, 500, ct)).Value;
        var failures = rows.Select(static e => TryParse(e.Envelope.Payload))
            .OfType<JsonElement>()
            .Where(static p => p.TryGetProperty("trigger", out var t) && string.Equals(t.GetString(), "step.failed", StringComparison.Ordinal))
            .Select(static p => p.GetProperty("detail").GetProperty("reason").GetString())
            .ToList();
        var decisions = DecisionFacts();
        var why = await Why(decisions, ct);
        return $"the instance is in '{state}', not 'prove'. Jev was asked {jev.Requests.Count} time(s): {string.Join("; ", jev.Requests)}. step.failed reasons: [{string.Join(" | ", failures)}]. decision facts: [{string.Join(" | ", decisions)}]. judge why: {why}";
    }

    // Every stored fact that is a judge decision (it has a "choice").
    private List<string> DecisionFacts() =>
        [.. Directory.EnumerateFiles(Path.Combine(_harness.Home, "workflow", "facts")).Select(static f => File.ReadAllText(f)).Where(static t => t.Contains("\"choice\"", StringComparison.Ordinal))];

    // `judge why <decisionId>` on the judge's own store (the directory JudgeCliChoice hands the child: <Sindri home>/judge), which names why Jev was skipped.
    private async Task<string> Why(List<string> decisions, CancellationToken ct)
    {
        var id = decisions.Select(static t => TryParse(t)).OfType<JsonElement>()
            .Select(static e => e.TryGetProperty("decisionId", out var d) ? d.GetString() : null).FirstOrDefault(static d => !string.IsNullOrEmpty(d));
        if (id is null)
        {
            return "(no decision id in the decision facts)";
        }

        var start = new System.Diagnostics.ProcessStartInfo(Environment.GetEnvironmentVariable("AW_SINDRI_JUDGE")!, ["why", id]) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };   // why: the test skipped without AW_SINDRI_JUDGE
        start.Environment["AW_JUDGE_STATE_DIR"] = Path.Combine(_harness.Home, "judge");
        using var process = System.Diagnostics.Process.Start(start)!;   // why: a file that exists starts
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(TimeSpan.FromSeconds(20));
        var output = await process.StandardOutput.ReadToEndAsync(limit.Token);
        var error = await process.StandardError.ReadToEndAsync(limit.Token);
        await process.WaitForExitAsync(limit.Token);
        return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"exit {process.ExitCode}: {output} {error}").ReplaceLineEndings(" ");
    }

    // Fail fast: none of the agent CLIs the default list falls back to is reachable from the PATH the judge child inherits.
    private static void AssertNoAgentCli()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        var found = path.Split(':', StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(static dir => _agentClis.Select(name => Path.Combine(dir, name)))
            .FirstOrDefault(static file => File.Exists(file));
        if (found is not null)
        {
            Assert.Fail($"an agent CLI is on the test's PATH: {found} (PATH={path})");
        }
    }

    // The decision the Step wrote ('fact:decision' is stored in the home's fact store): the provider that answered.
    private string? ProviderOfDecision() =>
        Directory.EnumerateFiles(Path.Combine(_harness.Home, "workflow", "facts"))
            .Select(static file => TryParse(File.ReadAllText(file)))
            .OfType<JsonElement>()
            .Where(static e => e.ValueKind == JsonValueKind.Object && e.TryGetProperty("decisionId", out _))
            .Select(static e => e.TryGetProperty("provider", out var p) ? p.GetString() : null)
            .FirstOrDefault();

    private static JsonElement? TryParse(string text)
    {
        try
        {
            return JsonDocument.Parse(text).RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private string Temp()
    {
        var dir = Directory.CreateTempSubdirectory("aw-judge-images-e2e-").FullName;
        _dirs.Add(dir);
        return dir;
    }

    // A PNG header for a 1512x982 picture (the judge reads sizes from headers; Jev here is a stand-in).
    private static string Frame(string path)
    {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);   // why: a path with a directory
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52, 0, 0, 0x05, 0xE8, 0, 0, 0x03, 0xD6, 8, 2, 0, 0, 0, 0, 0, 0, 0];
        File.WriteAllBytes(path, png);
        return path;
    }

    private static string Recipe() => string.Join('\n',
        [
            "schema: 1",
            "id: " + RecipeId,
            "version: 1",
            "inputs:",
            "  - { key: fact:change, label: \"Change\", type: text, kind: text }",
            "  - { key: fact:frames, label: \"Frames\", type: text, kind: text }",
            "  - { key: fact:item, label: \"Item\", type: item, kind: item }",
            "initial: route",
            "states:",
            "  route:",
            "    kind: active",
            "    step:",
            "      use: step.judge.choose",
            "      with:",
            "        prompt: \"Which evidence proves this change works?\"",
            "        facts: [fact:change]",
            "        images: \"fact:frames\"",
            "        choices:",
            "          - \"ui: what a user sees changed\"",
            "          - \"api: a contract changed\"",
            "    timeout: { after: 2d, then: park }",
            "    next: \"Pick the evidence.\"",
            "  route-desk:",
            "    kind: active",
            "    step: { use: step.desk.choose, with: { from: route } }",
            "    timeout: { after: 2d, then: park }",
            "    next: \"Pick the evidence.\"",
            "  prove: { kind: active, step: step.produce, timeout: { after: 1d, then: park }, next: \"Prove it.\" }",
            "  done: { kind: terminal, outcome: resolved }",
            "transitions:",
            "  - { from: [route, route-desk], to: prove, on: step.completed, when: { outcome: ui }, produces: [fact:decision] }",
            "  - { from: [route, route-desk], to: prove, on: step.completed, when: { outcome: api }, produces: [fact:decision] }",
            "  - { from: route, to: route-desk, on: step.completed, when: { outcome: uncertain }, produces: [fact:decision] }",
            "  - { from: route, to: route-desk, on: step.failed }",
            "  - { from: prove, to: done, on: step.completed, when: { outcome: done }, produces: [fact:output] }",
            "evals: []",
        ]);

    // Jev on loopback: answers every request 'ui' at 0.95, and keeps each request's media type and how many image parts it carried.
    private sealed class RecordingJev : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly CancellationTokenSource _stop = new();
        private readonly List<Asked> _requests = [];

        private RecordingJev(int port)
        {
            Url = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}/v1/systemone");
            _listener.Prefixes.Add(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}/"));
            _listener.Start();
            _ = Task.Run(Serve, _stop.Token);
        }

        public string Url { get; }

        public IReadOnlyList<Asked> Requests
        {
            get
            {
                lock (_requests)
                {
                    return [.. _requests];
                }
            }
        }

        public static RecordingJev Start()
        {
            using var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return new RecordingJev(port);
        }

        public void Dispose()
        {
            _stop.Cancel();
            _listener.Close();
            _stop.Dispose();
        }

        private async Task Serve()
        {
            const string Answer = """{"answers":{"decision":{"type":"choice","choice":"ui","confidence":0.95}},"usage":{"input_tokens":10,"output_tokens":1}}""";
            while (!_stop.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    using var body = new MemoryStream();
                    await context.Request.InputStream.CopyToAsync(body, _stop.Token);
                    var text = Encoding.Latin1.GetString(body.ToArray());
                    lock (_requests)
                    {
                        _requests.Add(new Asked(context.Request.ContentType?.Split(';')[0] ?? "", Count(text, "Content-Type: image/png")));
                    }

                    var bytes = Encoding.UTF8.GetBytes(Answer);
                    context.Response.ContentType = "application/json";
                    await context.Response.OutputStream.WriteAsync(bytes, _stop.Token);
                    context.Response.Close();
                }
                catch (Exception e) when (e is HttpListenerException or ObjectDisposedException or InvalidOperationException)
                {
                    return;   // boundary: the listener was closed by the test's end
                }
            }
        }

        private static int Count(string text, string needle)
        {
            var count = 0;
            for (var at = text.IndexOf(needle, StringComparison.OrdinalIgnoreCase); at >= 0; at = text.IndexOf(needle, at + needle.Length, StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }

            return count;
        }
    }

    private sealed record Asked(string ContentType, int ImageParts);
}
