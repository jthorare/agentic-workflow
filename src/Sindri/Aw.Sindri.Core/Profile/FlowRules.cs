using System.Globalization;
using Aw.Kernel;

namespace Aw.Sindri.Core.Profile;

/// <summary>The workflow flow keys (agent-first PR 9): 'workflow.callDepth' is 1 to 10 and 'workflow.http.hosts' are plain public host names.</summary>
internal static class FlowRules
{
    /// <summary>The most hosts the allowlist holds.</summary>
    internal const int MaxHosts = 50;

    /// <summary>The deepest 'workflow.callDepth' may be set to.</summary>
    internal const int MaxCallDepth = 10;

    internal static Error? Check(WorkflowSettings? workflow) => CallDepth(workflow?.CallDepth) ?? Hosts(workflow?.Http?.Hosts) ?? Editor(workflow?.Editor);

    /// <summary>Whether <paramref name="host"/> is a lower-case DNS name with a dot, whose last label has a letter (so no IP address), and is not under 'localhost'.</summary>
    /// <param name="host">The name.</param>
    /// <returns>True when an allowlist may hold it.</returns>
    internal static bool IsPublicHostName(string host)
    {
        if (host.Length is 0 or > 253 || host.EndsWith(".localhost", StringComparison.Ordinal))
        {
            return false;
        }

        var labels = host.Split('.');
        return labels.Length >= 2 && labels.All(IsLabel) && labels[^1].Any(char.IsAsciiLetter);
    }

    private static bool IsLabel(string label) =>
        label.Length is > 0 and <= 63 && label.All(static c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-') && label[0] != '-' && label[^1] != '-';

    /// <summary>The values 'workflow.editor' takes; null means <see cref="SheetEditor"/>.</summary>
    internal static readonly string[] Editors = [SheetEditor, StepsEditor];

    /// <summary>The sheet-first editor, the default.</summary>
    internal const string SheetEditor = "sheet";

    /// <summary>The steps editor.</summary>
    internal const string StepsEditor = "steps";

    private static Error? Editor(string? editor) =>
        editor is null || Editors.Contains(editor, StringComparer.Ordinal)
            ? null
            : Invalid("workflow.editor", $"must be 'sheet' or 'steps', not '{editor}'");

    private static Error? CallDepth(int? depth) =>
        depth is { } set && set is < 1 or > MaxCallDepth
            ? Invalid("workflow.callDepth", string.Create(CultureInfo.InvariantCulture, $"must be a whole number from 1 to {MaxCallDepth}"))
            : null;

    private static Error? Hosts(IReadOnlyList<string>? hosts)
    {
        if (hosts is null)
        {
            return null;
        }

        if (hosts.Count > MaxHosts)
        {
            return Invalid("workflow.http.hosts", string.Create(CultureInfo.InvariantCulture, $"holds at most {MaxHosts} hosts"));
        }

        for (var i = 0; i < hosts.Count; i++)
        {
            var at = string.Create(CultureInfo.InvariantCulture, $"workflow.http.hosts[{i}]");
            if (!IsPublicHostName(hosts[i]))
            {
                return Invalid(at, "must be a lower-case host name such as api.example.com (no scheme, port or path, no IP address, no localhost)");
            }

            if (hosts.Take(i).Contains(hosts[i], StringComparer.Ordinal))
            {
                return Invalid(at, $"'{hosts[i]}' is listed twice");
            }
        }

        return null;
    }

    private static Error Invalid(string keyPath, string problem) => SindriErrors.ProfileInvalid.With($"{keyPath}: {problem}");
}
