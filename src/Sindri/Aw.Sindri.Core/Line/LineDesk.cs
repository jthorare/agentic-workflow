using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Line;

/// <summary>What a line station's block Steps report at the desk (line spec §4.1, §6.1): the outcome words, the findings fact and the packet files under their directory.</summary>
public static class LineDesk
{
    /// <summary>A check Step's report: 'pass', or 'gaps' with the findings fact.</summary>
    /// <param name="block">The full Step id.</param>
    /// <param name="response">The draft-stage result.</param>
    /// <returns>The report.</returns>
    public static LineDeskResponse Check(string block, LineCheckResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return response.Passed
            ? new LineDeskResponse(block, "pass", new Dictionary<string, string>(StringComparer.Ordinal), [], [], response.Warnings)
            : Gaps(block, "gaps", response);
    }

    /// <summary>An evidence Step's report when its checks did not complete: 'incomplete', with a finding for each failing rule and each rule that could not run (never a pass at evidence).</summary>
    /// <param name="block">The full Step id.</param>
    /// <param name="response">The evidence-stage result.</param>
    /// <returns>The report.</returns>
    public static LineDeskResponse Incomplete(string block, LineCheckResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        var unrun = response.Rules
            .Where(static r => r.Status == LineRuleStatus.NotRun && r.Missing.Count > 0)
            .Select(static r => new LineFinding(r.Rule, Item: null, $"{r.Rule} could not run: it needs {string.Join(", ", r.Missing)}", $"produce {string.Join(", ", r.Missing)}", LineSeverity.Fail));
        return Gaps(block, "incomplete", response with { Findings = [.. response.Findings, .. unrun] });
    }

    /// <summary>An evidence Step's report when its checks completed: 'complete', with the candidate packet's files under <paramref name="directory"/>.</summary>
    /// <param name="block">The full Step id.</param>
    /// <param name="response">The evidence-stage result.</param>
    /// <param name="directory">The packet directory in the run area, for example 'design-packet'.</param>
    /// <param name="files">The packet's files.</param>
    /// <returns>The report.</returns>
    public static LineDeskResponse Complete(string block, LineCheckResponse response, string directory, IReadOnlyList<LineFile> files)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(files);
        return new LineDeskResponse(block, "complete", new Dictionary<string, string>(StringComparer.Ordinal), [.. files.Select(f => f with { Path = $"{directory}/{f.Path}" })], [], response.Warnings);
    }

    private static LineDeskResponse Gaps(string block, string outcome, LineCheckResponse response)
    {
        var findings = new JsonArray();
        foreach (var finding in response.Findings)
        {
            LineJson.Append(findings, new JsonObject { ["rule"] = finding.Rule, ["item"] = finding.Item, ["message"] = finding.Message, ["fix"] = finding.Fix, ["severity"] = "fail" });
        }

        var outputs = new Dictionary<string, string>(StringComparer.Ordinal) { ["findings"] = LineJson.Write(findings).TrimEnd('\n') };
        return new LineDeskResponse(block, outcome, outputs, [], response.Findings, response.Warnings);
    }
}
