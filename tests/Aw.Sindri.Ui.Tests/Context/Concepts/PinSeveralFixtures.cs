using System.Net;
using System.Text;
using Aw.Kernel.Json;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context.Concepts;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context.Concepts;

/// <summary>What the Add several tests share: a fake server with the pins, profile, suggest and job reads, and a sheet over it with a fake clock.</summary>
internal sealed class PinSeveralFixtures
{
    internal const string JobId = "JOB-00000000000000000000000000000007";
    internal const string FileHash = "file-h0";

    private static readonly MapRebuildView _idle = new("idle", Progress: null, RetryAt: null, Stale: false, Failed: null);

    internal PinSeveralFixtures(IReadOnlyList<PinView>? pins = null, params string[] waiting)
    {
        Pins = pins ?? [];
        Api = new FakeSindriApi
        {
            ProfileAnswer = Profile("h1", waiting),
            PinConceptsAnswer = new RouteAnswer(HttpStatusCode.OK, $$"""{"accepted":{"commandId":"00000000-0000-0000-0000-000000000009","sequence":12},"jobId":"{{JobId}}"}"""),
            ContextPinsAnswer = _ => Served(new PinsView(1, "api", FileHash, "v1", Pins, _idle)),
            ContextPinSuggestAnswer = Suggest,
        };
        Api.JobAnswers[JobId] = new RouteAnswer(HttpStatusCode.OK, Job("succeeded", code: null, summary: null, hash: "h1"));
        Clock = new FakeTimeProvider(A.Noon);
        Url = new Uri("http://127.0.0.1:1/");
    }

    internal FakeSindriApi Api { get; }

    internal FakeTimeProvider Clock { get; }

    internal Uri Url { get; }

    internal IReadOnlyList<PinView> Pins { get; }

    /// <summary>What a pasted-list suggest call answers: one entry per line, the concept key for the line's name (null for none).</summary>
    internal Dictionary<string, string?> Matches { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>What a concept= suggest call answers, by concept key; a concept not here answers a 404.</summary>
    internal Dictionary<string, PinSuggestView> Concepts { get; } = new(StringComparer.Ordinal);

    /// <summary>Whether the pasted-list suggest call fails.</summary>
    internal bool MatchFails { get; set; }

    internal static CancellationToken Token => TestContext.Current.CancellationToken;

    internal IReadOnlyList<ConceptTileViewModel> Layer { get; set; } =
    [
        Card("concept:billing", "Billing"), Card("concept:refunds", "Refunds"), Card("concept:notify", "Notify"), Card("concept:other", "Other"),
    ];

    internal static ConceptTileViewModel Card(string id, string name) =>
        new(id, name, summary: string.Empty, new ConceptSize(1, 1, 1, 1), setByPerson: false, terms: null, _ => { });

    internal static PinView Pin(string id, string name, params string[] aliases) => new(id, name, Summary: null, aliases, [], FolderRules: 0, SetIn: "profile", PinStates.Approved, Change: null);

    internal static RouteAnswer Profile(string live, params string[] changed) =>
        new(HttpStatusCode.OK, $$"""{"approved":"h0","live":"{{live}}","changed":true,"changedPaths":[{{string.Join(',', changed.Select(c => $"\"{c}\""))}}]}""");

    internal static RouteAnswer Served(PinsView view) => new(HttpStatusCode.OK, Encoding.UTF8.GetString(AwJson.Serialize(view, SindriApiClient.Json)));

    internal static string Job(string state, string? code, string? summary, string hash) =>
        $$"""{"id":"{{JobId}}","kind":"PinConcepts","key":"k","item":null,"repo":null,"service":null,"state":"{{state}}","startedBy":"human","startedAt":"2026-10-11T12:00:00+00:00","endedAt":null,"progress":[],"rounds":0,"summary":{{Quoted(summary)}},"code":{{Quoted(code)}},"result":{"hash":"{{hash}}","written":true,"approved":false},"cancelledBy":null,"actions":[],"cli":null}""";

    internal static PinSuggestView Terms(params PinSuggestTerm[] terms) => new(1, "api", "v1", terms, Features: null, Matches: null);

    internal static PinSuggestView WithFeatures(PinSuggestTerm[] terms, params PinSuggestFeature[] features) => new(1, "api", "v1", terms, features, Matches: null);

    /// <summary>The profile file hash the page's pins read gave the sheet; null when the page had none.</summary>
    internal string? OpenFileHash { get; set; } = FileHash;

    internal PinSheetOpen Open() => new("api", Card: null, Pin: null, Pins, Several: true, FileHash: OpenFileHash);

    internal PinSeveralViewModel Sheet(Action? close = null) =>
        new(Open(), Layer, Api, () => Url, new ProfileWriteFlow(Api, () => Url, Clock), close ?? (() => { }));

    // Starts the sheet, pastes the list and reads it.
    internal async Task<PinSeveralViewModel> Read(string list, Action? close = null)
    {
        var sheet = Sheet(close);
        await sheet.StartAsync(Token);
        sheet.Text = list;
        await sheet.ReadListAsync(Token);
        return sheet;
    }

    private static string Quoted(string? text) => text is null ? "null" : $"\"{text}\"";

    private RouteAnswer Suggest(PinSuggestQuery query)
    {
        if (query.Lines is { } lines)
        {
            return MatchFails
                ? RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-API-001", "The control plane is restarting.")
                : AConcepts.Served(new PinSuggestView(1, "api", "v1", Terms: null, Features: null, [.. lines.Select((l, i) => Match(i, l.Name))]));
        }

        return query.Concept is { } key && Concepts.TryGetValue(key, out var view) ? AConcepts.Served(view) : RouteAnswer.Problem(HttpStatusCode.NotFound, "SND-CONTEXT-179", "not served");
    }

    private PinSuggestMatch Match(int line, string name)
    {
        var key = Matches.TryGetValue(name, out var found) ? found : null;
        return new PinSuggestMatch(line, key, key is null ? null : Layer.FirstOrDefault(c => string.Equals(c.Id, key, StringComparison.Ordinal))?.Name);
    }
}
