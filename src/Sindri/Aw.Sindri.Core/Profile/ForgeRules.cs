using System.Globalization;
using Aw.Kernel;

namespace Aw.Sindri.Core.Profile;

/// <summary>
/// The forge keys (PR babysitter spec W27): 'adapter' is github or file, 'hosts' are plain host names, and 'credential' is
/// env:NAME or command:argv (the same forms the forge package parses; the value is never echoed back).
/// </summary>
internal static class ForgeRules
{
    internal static Error? Check(ForgeSettings? forge)
    {
        if (forge is null)
        {
            return null;
        }

        if (forge.Adapter is { } adapter && adapter is not ("github" or "file"))
        {
            return Invalid("workflow.forge.adapter", "must be 'github' or 'file'");
        }

        return Hosts(forge.Hosts ?? []) ?? Credential(forge.Credential);
    }

    private static Error? Hosts(IReadOnlyList<string> hosts)
    {
        for (var i = 0; i < hosts.Count; i++)
        {
            if (hosts[i].Length == 0 || !hosts[i].All(static c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-'))
            {
                return Invalid(string.Create(CultureInfo.InvariantCulture, $"workflow.forge.hosts[{i}]"), "must be a host name such as github.com (letters, digits, '.' and '-')");
            }
        }

        return null;
    }

    private static Error? Credential(string? credential) =>
        credential is not null && !IsCredential(credential)
            ? Invalid("workflow.forge.credential", "must be env:NAME (capitals, digits and '_') or command:<program and arguments>")
            : null;

    private static bool IsCredential(string value) =>
        value.StartsWith("env:", StringComparison.Ordinal)
            ? value.Length > 4 && value[4..].All(static c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c) || c == '_')
            : value.StartsWith("command:", StringComparison.Ordinal) && value[8..].Trim().Length > 0;

    private static Error Invalid(string keyPath, string problem) =>
        SindriErrors.ProfileInvalid.With($"{keyPath}: {problem}");
}
