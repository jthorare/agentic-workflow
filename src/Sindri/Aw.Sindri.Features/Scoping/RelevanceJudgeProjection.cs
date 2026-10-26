using System.Text.Json.Nodes;
using Aw.Sindri.Core.Judge;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Features.Scoping;

/// <summary>
/// What the relevance judge sees of one entry (spec 4.2), built from code and nothing else: the brief's text, the entry's lens, key, kind, label, band and lens item id, and the lens's anchors. It never holds the agent's
/// class, reason, citation, fix or model, another entry's verdict, the entry's neighbours or edges, the coverage, or the order of the agent's file: those facts are not read here, so they cannot leak into a request.
/// </summary>
internal static class RelevanceJudgeProjection
{
    /// <summary>The longest text a request holds per fact.</summary>
    internal const int TextCap = 200_000;

    private const string Prompt = "Decide whether the entry, which the context map returned for the change described in the brief, belongs to that change. Answer with exactly one of the choices.";

    /// <summary>The three classes and when to pick each; the same words the review agent is given.</summary>
    internal static IReadOnlyList<ChooseOption> Choices { get; } =
    [
        new("in", "the entry belongs to the change in the brief"),
        new("out", "the entry is real and relevant to something, but not to this change"),
        new("noise", "the entry should never count toward scope, such as a shared shell, a primitive, an icon, a router, a cron, a test, a demo, or a cluster whose label does not match its members"),
    ];

    /// <summary>The judge request for one entry, scrubbed and capped.</summary>
    /// <param name="entry">The entry.</param>
    /// <param name="briefText">The brief's text.</param>
    /// <param name="anchors">The anchors of the entry's lens.</param>
    /// <param name="settings">The threshold and providers of the call.</param>
    /// <param name="scrubber">Redacts the text before it leaves.</param>
    /// <returns>The request.</returns>
    internal static ChooseRequest Request(RelevanceEntry entry, string briefText, IReadOnlyList<string> anchors, JudgeSettings settings, ITextScrubber scrubber)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(scrubber);
        var facts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["brief"] = Cap(scrubber.Scrub(briefText)),
            ["entry"] = Cap(scrubber.Scrub(ScopeJson.Write(Entry(entry)))),
            ["anchors"] = Cap(scrubber.Scrub(ScopeJson.Write(ScopeJson.ToArray(anchors)))),
        };
        return new ChooseRequest(Prompt, Choices, facts, settings.Threshold, settings.Providers);
    }

    private static JsonObject Entry(RelevanceEntry entry)
    {
        var shown = new JsonObject { ["lensItem"] = entry.Id, ["lens"] = entry.Lens, ["kind"] = entry.Kind, ["label"] = entry.Label };
        if (entry.Key is { } key)
        {
            shown["key"] = key;
        }

        if (entry.Band is { } band)
        {
            shown["band"] = band;
        }

        return shown;
    }

    private static string Cap(string text) => text.Length <= TextCap ? text : text[..TextCap];

    /// <summary>What a judge call is asked with besides the entry.</summary>
    /// <param name="Threshold">The confidence an answer needs.</param>
    /// <param name="Providers">The providers the judge may ask.</param>
    internal sealed record JudgeSettings(double Threshold, IReadOnlyList<string> Providers);
}
