using System.Text;
using System.Text.Json;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Testing.Checks;
using Aw.Sindri.Testing.Forge;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Effects;

/// <summary>The pull-request effects' fixture: a fake forge holding one open pull request, the facts a babysitter holds for it and the context an effect runs from.</summary>
internal sealed class PrEffectRig
{
    public const string Instance = "T-4/pr-babysitter/1";
    public const string Workspace = "/work/pr";
    public const string Seen = "aaa111";

    public PrEffectRig()
    {
        Pull = Forge.Add(new PullRepo("example.test", "o", "r"), "feat/x", Seen);
        Diff.Add(Workspace, Seen);
    }

    public FakePullRequests Forge { get; } = new(["example.test"]);

    public FakePull Pull { get; }

    public InMemoryFactStore Facts { get; } = new();

    public FakeRunDiff Diff { get; } = new();

    public InMemoryBranchChanges Changes { get; } = new();

    public string Link => Pull.Link;

    /// <summary>The context of an effect with the given literal and fact arguments (a fact argument is the stored fact, as the engine resolves it).</summary>
    public EffectContext Context(string effect, IReadOnlyDictionary<string, object> arguments, IReadOnlyDictionary<string, string>? facts = null)
    {
        var resolved = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var (name, value) in arguments)
        {
            resolved[name] = value is FactText fact ? Stored(fact.Json) : Parse("\"" + value + "\"");
        }

        var held = new Dictionary<FactKey, JsonElement>();
        foreach (var (key, json) in facts ?? new Dictionary<string, string>(StringComparer.Ordinal))
        {
            held[FactKey.From(key)] = Stored(json);
        }

        return new EffectContext(BlockId.From(effect), Instance + ":3:0", "c-1", FromEngine: false, Instance, "T-4", Attempt: 1, resolved, held, Text: null);
    }

    public static FactText Fact(string json) => new(json);

    public static string PrState(string head, string checksSha) => $$$"""{"head":{"ref":"feat/x","sha":"{{{head}}}"},"checks":{"state":"failing","sha":"{{{checksSha}}}","failing":["unit"]}}""";

    private JsonElement Stored(string json)
    {
        var put = Facts.Put(Encoding.UTF8.GetBytes(json), CancellationToken.None).AsTask().GetAwaiter().GetResult().Value;
        return Parse($$"""{"ref":"{{put.Ref}}","sha256":"{{put.Sha256}}"}""");
    }

    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    /// <summary>A fact argument's JSON content.</summary>
    /// <param name="Json">The content.</param>
    public sealed record FactText(string Json);
}
