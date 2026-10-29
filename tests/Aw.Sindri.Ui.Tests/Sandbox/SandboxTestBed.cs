using System.Net;
using System.Text.Json.Nodes;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Sandbox;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Sandbox;

/// <summary>The sandbox card over the fake server, a clock at the goldens' day, and the states.json rows the tests read their words from.</summary>
internal sealed class SandboxTestBed
{
    internal static readonly Uri Api = new("http://127.0.0.1:7410/");
    internal static readonly DateTimeOffset Now = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);

    internal SandboxTestBed()
    {
        Card = new SandboxCardViewModel(Server, Clock, () => Api, new InstanceCommander(Server, () => Api).Send, Clipboard);
    }

    internal FakeSindriApi Server { get; } = new();

    internal FakeTimeProvider Clock { get; } = new(Now);

    internal FakeClipboard Clipboard { get; } = new();

    internal SandboxCardViewModel Card { get; }

    internal SandboxCheckViewModel Check => Card.Check;

    internal static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A states.json row by section and id.</summary>
    /// <param name="section">'healthRow', 'checkAgain', 'parkCard', 'settingsSection'.</param>
    /// <param name="id">The row's id.</param>
    /// <returns>The row.</returns>
    internal static JsonNode Row(string section, string id) =>
        JsonNode.Parse(GoldenFiles.Sandbox("states.json"))![section]!.AsArray().Single(r => (string)r!["id"]! == id)!;

    internal static string Text(string section, string id) => (string)Row(section, id)["text"]!;

    internal static string Announce(string section, string id) => (string)Row(section, id)["announce"]!;

    /// <summary>A check answer with these blockers and Steps.</summary>
    internal static RouteAnswer CheckBody(string[] blockers, string[] titles, DateTimeOffset? checkedAt = null, string[]? notChecked = null) =>
        Body(checkedAt ?? Now, blockers, titles, notChecked ?? []);

    private static RouteAnswer Body(DateTimeOffset at, string[] blockers, string[] titles, string[]? notChecked = null) =>
        new(HttpStatusCode.OK,
            $$"""{"blockers":[{{string.Join(',', blockers.Select((b, i) => $$"""{"code":"SND-LAUNCH-014","reason":"r{{i.ToString(System.Globalization.CultureInfo.InvariantCulture)}}","text":"{{b}}","command":"fix {{i.ToString(System.Globalization.CultureInfo.InvariantCulture)}}"}"""))}}],"ready":{{(blockers.Length == 0 ? "true" : "false")}},"resume":[{{string.Join(',', titles.Select((t, i) => $$"""{"instance":"{{Instance(i)}}","item":"ITM-{{Hex(i)}}","title":"{{t}}","since":"2026-10-28T08:00:00.0000000Z"}"""))}}],"notChecked":[{{string.Join(',', (notChecked ?? []).Select(n => "\"" + n + "\""))}}],"checkedAt":"{{at:O}}"}""");

    private static string Hex(int i) => i.ToString("x32", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>A sequence on one line, so a list compares as a string.</summary>
    internal const string HooksNotChecked = "The hooks weren't checked: no agent is picked for Steps.";

    internal static string J(IEnumerable<string?> values) => string.Join(" | ", values);

    internal static string Instance(int i) => "ITM-" + i.ToString("x32", System.Globalization.CultureInfo.InvariantCulture) + "/step/1";
}
