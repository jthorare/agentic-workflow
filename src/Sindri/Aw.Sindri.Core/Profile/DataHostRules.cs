using System.Globalization;
using Aw.Kernel;

namespace Aw.Sindri.Core.Profile;

/// <summary>The data blocks' host keys (PR 9b): 'workflow.news.hosts' and 'workflow.vulnerabilities.hosts' are plain host names.</summary>
internal static class DataHostRules
{
    internal static Error? Check(WorkflowSettings? workflow) =>
        Hosts("workflow.news.hosts", workflow?.News?.Hosts) ?? Hosts("workflow.vulnerabilities.hosts", workflow?.Vulnerabilities?.Hosts);

    private static Error? Hosts(string key, IReadOnlyList<string>? hosts)
    {
        if (hosts is null)
        {
            return null;
        }

        for (var i = 0; i < hosts.Count; i++)
        {
            if (hosts[i].Length == 0 || !hosts[i].All(static c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-'))
            {
                return SindriErrors.ProfileInvalid.With(string.Create(CultureInfo.InvariantCulture, $"{key}[{i}]: must be a host name such as hacker-news.firebaseio.com (letters, digits, '.' and '-')"));
            }
        }

        return null;
    }
}
