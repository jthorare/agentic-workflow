using System.Text.RegularExpressions;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;
using Aw.Context.Core.Trust;

namespace Aw.Context.Core.Map;

/// <summary>
/// The map's text as answers write it (spec §7): every label, wording and quote crosses this call's ingest boundary
/// (sanitize, then the built-in rules and the profile's extra patterns), then the egress scrubber, one field at a time.
/// Labels are one line of at most 200 characters; untrusted text (model output not yet accepted, quotes) goes inside the
/// fence. The map's text is cleaned on the way out with the current rules, so a change to the patterns applies to the
/// next answer; nothing cleaned is stored, so there is no ruleset to record.
/// </summary>
/// <param name="scrubbers">This call's scrubbers.</param>
/// <param name="policy">Whether untrusted text is fenced (<see cref="FencePolicies.For"/> picks it from the caller).</param>
public sealed partial class MapText(Scrubbers scrubbers, FencePolicy policy)
{
    private const string Blocked = "[REDACTED:blocked]";
    private const string Source = "map";

    /// <summary>Text that fences untrusted labels, as every answer did before the app: queries, and any caller that has not asked for clean text.</summary>
    /// <param name="scrubbers">This call's scrubbers.</param>
    public MapText(Scrubbers scrubbers)
        : this(scrubbers, FencePolicy.Fenced)
    {
    }

    /// <summary>A label: one line, at most 200 characters, fenced when untrusted.</summary>
    /// <param name="label">The label as stored.</param>
    /// <param name="trust">Its trust.</param>
    /// <returns>The text to write.</returns>
    public string Label(string label, Results.Trust trust) => Fenced(UntrustedFence.Cap(Clean(label, singleLine: true)), trust);

    /// <summary>A longer text (a quote), fenced when untrusted.</summary>
    /// <param name="text">The text as stored.</param>
    /// <param name="trust">Its trust.</param>
    /// <returns>The text to write.</returns>
    public string Body(string text, Results.Trust trust) => Fenced(Clean(text, singleLine: false), trust);

    /// <summary>Text cleaned like every answer's but not fenced: what the search index holds, so the fence is added once, on the way out.</summary>
    /// <param name="text">The text as stored.</param>
    /// <param name="singleLine">True to make it one line of at most 200 characters.</param>
    /// <returns>The cleaned text.</returns>
    public string Cleaned(string text, bool singleLine) => singleLine ? UntrustedFence.Cap(Clean(text, singleLine: true)) : Clean(text, singleLine: false);

    /// <summary>A short plain-text summary of a longer text: one line, cut on a word to at most <paramref name="max"/> characters with '…', fenced when untrusted. It is scrubbed before the cut, so a cut never splits a secret the scrubber would have caught.</summary>
    /// <param name="text">The text as stored.</param>
    /// <param name="trust">Its trust.</param>
    /// <param name="max">The most characters kept, the ellipsis included.</param>
    /// <returns>The text to write.</returns>
    public string Excerpt(string text, Results.Trust trust, int max) => Fenced(UntrustedFence.CapOnWord(Clean(text, singleLine: true), max), trust);

    /// <summary>The words a node-shape query searches the sources with: the cleaned label's words, minus the placeholders that cleaning left (egress only, so a planted secret never reaches a source).</summary>
    /// <param name="label">The label as stored.</param>
    /// <returns>The search terms (<see cref="QueryRules.Terms"/>).</returns>
    public IReadOnlyList<string> SearchTerms(string label) => QueryRules.Terms(Placeholder.Replace(Clean(label, singleLine: true), " "));

    /// <summary>A line of keys and edge names: egress only (no markup to sanitize), never fenced.</summary>
    /// <param name="line">The line.</param>
    /// <returns>The scrubbed line.</returns>
    public string Line(string line) => scrubbers.Egress.Scrub(line) is { Blocked: false } scrubbed ? scrubbed.Text : Blocked;

    /// <summary>A node as an answer shows it: its label written by <see cref="Label"/>, confirmed when human, trust from provenance.</summary>
    /// <param name="node">The node.</param>
    /// <returns>The fact.</returns>
    public NodeFact Fact(GraphNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        var confirmed = node.Provenance == Provenance.Human;
        var trust = TrustPolicy.ForNode(node, confirmed);
        return new NodeFact(node.Key, node.Key.Kind, Label(node.Label, trust), node.Provenance, confirmed, trust, node.Confidence);
    }

    private string Clean(string text, bool singleLine)
    {
        var ingested = scrubbers.Ingest.Clean(text, singleLine);
        return ingested.Blocked || scrubbers.Egress.Scrub(ingested.Text) is not { Blocked: false } scrubbed ? Blocked : scrubbed.Text;
    }

    [GeneratedRegex("\\[REDACTED:[^\\]]*\\]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Placeholder { get; }

    private string Fenced(string text, Results.Trust trust) => trust == Results.Trust.Untrusted && policy == FencePolicy.Fenced ? UntrustedFence.Wrap(Source, author: null, text) : text;
}
