using System.Globalization;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.Blocks.Gate;

/// <summary>
/// 'gate.pr.secret-scan' (PB spec §10.1): SHIP-SECRET-001's check, the same code and patterns as BLD-SECRET-001 (the shared scrubber over Aw.Redaction), on the lines the push added
/// between the head the snapshot saw ('since') and the workspace's head. A hit refuses, naming file and line only, never the match. A gate that can't read the diff refuses too:
/// an unscanned push must not pass.
/// </summary>
/// <param name="content">Reads the lines the push added.</param>
/// <param name="scrubber">The shared secret patterns (Aw.Redaction): a line it changes holds a secret.</param>
internal sealed class PrSecretScanGate(IDiffContent content, ITextScrubber scrubber) : IGate, IDescribedBlock
{
    private const int MaxNamed = 20;

    /// <inheritdoc/>
    public static BlockDescriptor Descriptor { get; } = PrGates.SecretScan;

    /// <inheritdoc/>
    public async ValueTask<GateResult> Evaluate(GateContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (PrGateInputs.Workspace(context) is not { } workspace || PrGateInputs.SinceHead(context) is not { } since)
        {
            return Refused("gate.pr.secret-scan needs the workspace (a path) and the PR state's head sha, so it can't scan; nothing is pushed");
        }

        var added = await content.AddedLines(workspace, since, cancellationToken).ConfigureAwait(false);
        if (added.IsError)
        {
            return Refused($"gate.pr.secret-scan can't read the diff of {workspace} since {since}: {added.Error.Message}");
        }

        var hits = added.Value.Where(line => line.Text.Length > 0 && !string.Equals(scrubber.Scrub(line.Text), line.Text, StringComparison.Ordinal)).ToList();
        if (hits.Count == 0)
        {
            return new GateResult(GateVerdict.Pass, Score: null, Reasons: []);
        }

        var named = string.Join(", ", hits.Take(MaxNamed).Select(static h => $"{h.Path}:{h.Line.ToString(CultureInfo.InvariantCulture)}"));
        var more = hits.Count > MaxNamed ? $" and {(hits.Count - MaxNamed).ToString(CultureInfo.InvariantCulture)} more" : string.Empty;
        return Refused($"SHIP-SECRET-001: {hits.Count.ToString(CultureInfo.InvariantCulture)} added line(s) hold a secret: {named}{more}; remove it and amend before the push");
    }

    private static GateResult Refused(string reason) => new(GateVerdict.Fail, Score: null, [reason]);
}
