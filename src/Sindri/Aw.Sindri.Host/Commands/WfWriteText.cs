using System.Globalization;
using System.Text;
using Aw.Sindri.Features.ValidateWorkflow;

namespace Aw.Sindri.Host.Commands;

/// <summary>The text of 'sindri wf validate --slug' (without --json).</summary>
internal static class WfWriteText
{
    internal static string Validation(ValidateWorkflowResponse answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        var text = new StringBuilder();
        _ = text.Append(CultureInfo.InvariantCulture, $"{(answer.Passed ? "passed" : "has errors")}{(answer.Hash is null ? string.Empty : "  hash " + answer.Hash)}\n");
        foreach (var f in answer.Findings)
        {
            var where = f.Nodes.Count == 0 ? string.Empty : $" node '{string.Join("', '", f.Nodes)}'{(f.Field is null ? string.Empty : $" field '{f.Field}'")}";
            _ = text.Append(CultureInfo.InvariantCulture, $"{f.Severity} {f.Code}{where}: {f.Message}; {f.Fix}\n");
        }

        return text.ToString();
    }
}
