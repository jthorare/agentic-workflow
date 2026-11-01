using System.Net;
using System.Text;
using Aw.Kernel.Json;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context.Concepts;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context.Concepts;

/// <summary>What the Pin sheet's tests share: a fake server with the profile, the job and the pins reads, and a sheet over it with a fake clock.</summary>
internal sealed class PinSheetFixtures
{
    internal const string JobId = "JOB-00000000000000000000000000000007";
    internal const string FileHash = "file-h0";

    internal PinSheetFixtures(params string[] waiting)
    {
        Api = new FakeSindriApi
        {
            ProfileAnswer = Profile("h1", waiting),
            PinConceptsAnswer = new RouteAnswer(HttpStatusCode.OK, $$"""{"accepted":{"commandId":"00000000-0000-0000-0000-000000000009","sequence":12},"jobId":"{{JobId}}"}"""),
            ContextPinsAnswer = _ => Served(new PinsView(1, "api", FileHash, "v1", [], _idle)),
        };
        Succeeds();
        Clock = new FakeTimeProvider(A.Noon);
        var url = new Uri("http://127.0.0.1:1/");
        Ports = new PinSheetPorts(Api, new ProfileWriteFlow(Api, () => url, Clock), () => url, Clock);
    }

    private static readonly MapRebuildView _idle = new("idle", Progress: null, RetryAt: null, Stale: false, Failed: null);

    internal FakeSindriApi Api { get; }

    internal FakeTimeProvider Clock { get; }

    internal PinSheetPorts Ports { get; }

    internal static CancellationToken Token => TestContext.Current.CancellationToken;

    internal static RouteAnswer Profile(string live, params string[] changed) =>
        new(HttpStatusCode.OK, $$"""{"approved":"h0","live":"{{live}}","changed":true,"changedPaths":[{{string.Join(',', changed.Select(c => $"\"{c}\""))}}]}""");

    internal static RouteAnswer Served(PinsView view) => new(HttpStatusCode.OK, Encoding.UTF8.GetString(AwJson.Serialize(view, SindriApiClient.Json)));

    internal static ConceptTileViewModel Card(string id = "billing", string name = "Billing", string summary = "How we take money", params string[] terms) =>
        new(id, name, summary, new ConceptSize(2, 3, 1, 40), setByPerson: false, terms, _ => { });

    internal static PinView Pin(string id, string name, string[]? aliases = null, string[]? features = null, int folderRules = 0, string state = PinStates.Approved) =>
        new(id, name, Summary: null, aliases ?? [], features ?? [], folderRules, "profile", state, Change: null);

    internal PinSheetViewModel Sheet() => new(Ports);

    internal void Pins(params PinView[] pins) =>
        Api.ContextPinsAnswer = _ => Served(new PinsView(1, "api", FileHash, "v1", pins, _idle));

    internal void Suggests(IReadOnlyList<PinSuggestTerm>? terms, IReadOnlyList<PinSuggestFeature>? features) =>
        Api.ContextPinSuggestAnswer = _ => AConcepts.Served(new PinSuggestView(1, "api", "v1", terms, features, Matches: null));

    /// <summary>The write's job ends well, with the hash the app approves.</summary>
    internal void Succeeds() => Api.JobAnswers[JobId] = new RouteAnswer(HttpStatusCode.OK, Job("succeeded", code: null, summary: null));

    /// <summary>The write's job ends with a server code.</summary>
    internal void Fails(string code, string summary) => Api.JobAnswers[JobId] = new RouteAnswer(HttpStatusCode.OK, Job("failed", code, summary));

    /// <summary>The write's job never ends.</summary>
    internal void Runs() => Api.JobAnswers[JobId] = new RouteAnswer(HttpStatusCode.OK, Job("running", code: null, summary: null));

    /// <summary>Lets the fake clock run until the task ends: the job poll waits on it.</summary>
    internal async Task Pump(Task task)
    {
        for (var i = 0; i < 2000 && !task.IsCompleted; i++)
        {
            Clock.Advance(TimeSpan.FromMilliseconds(250));
            await Task.Delay(1, Token);
        }

        await task.WaitAsync(Token);
    }

    private static string Job(string state, string? code, string? summary) =>
        $$"""{"id":"{{JobId}}","kind":"PinConcepts","key":"k","item":null,"repo":null,"service":null,"state":"{{state}}","startedBy":"human","startedAt":"2026-10-11T12:00:00+00:00","endedAt":null,"progress":[],"rounds":0,"summary":{{Quoted(summary)}},"code":{{Quoted(code)}},"result":{"pins":[],"hash":"h1","written":true,"approved":false,"diff":[]},"cancelledBy":null,"actions":[],"cli":null}""";

    private static string Quoted(string? text) => text is null ? "null" : $"\"{text}\"";
}
