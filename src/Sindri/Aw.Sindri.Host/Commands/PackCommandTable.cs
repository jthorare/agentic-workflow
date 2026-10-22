using System.Text;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Features.CheckPack;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// The 'pack' verb group (line spec §5.3, LN-API4, class local): 'pack check &lt;dir&gt; [--json]' checks a lens pack in this process, with no server. Exit 0 pass, 1 findings, 2 usage or a tool error.
/// </summary>
internal static class PackCommandTable
{
    private const string CheckUsage = "usage: sindri pack check <dir> [--json]";

    internal static Task<int> Run(string[] args, IServiceProvider services, Output output, CancellationToken ct) => args switch
    {
        ["check", var directory] when !directory.StartsWith('-') => Check(directory, json: false, services, output, ct),
        ["check", var directory, "--json"] when !directory.StartsWith('-') => Check(directory, json: true, services, output, ct),
        ["check", "--json", var directory] when !directory.StartsWith('-') => Check(directory, json: true, services, output, ct),
        ["check", ..] => output.Error(SindriErrors.InvalidInput.With($"pack check needs one pack directory ({CheckUsage}); --evals is not in this build")),
        _ => output.Error(SindriErrors.UnknownVerb.With($"'{string.Join(' ', ["pack", .. args])}' is not a sindri command ({CheckUsage})")),
    };

    private static async Task<int> Check(string directory, bool json, IServiceProvider services, Output output, CancellationToken ct)
    {
        var result = await services.GetRequiredService<IService<CheckPackRequest, CheckPackResponse>>().Handle(new CheckPackRequest(directory), ct).ConfigureAwait(false);
        if (result.IsError)
        {
            return await output.Error(result.Error).ConfigureAwait(false);
        }

        var code = result.Value.Passed ? 0 : 1;
        return json ? await output.Json(result.Value, code).ConfigureAwait(false) : await output.Text(Text(result.Value), code).ConfigureAwait(false);
    }

    /// <summary>The check as text: the findings, or the pack's lenses, hash and the pin line to approve.</summary>
    /// <param name="check">The check.</param>
    /// <returns>The text.</returns>
    internal static string Text(CheckPackResponse check)
    {
        ArgumentNullException.ThrowIfNull(check);
        var text = new StringBuilder();
        if (!check.Passed)
        {
            _ = text.Append("pack ").Append(check.Id?.Value ?? "(not a pack id)").AppendLine(" has findings:");
            foreach (var finding in check.Findings)
            {
                _ = text.Append("  ").Append(finding.Code).Append(' ').AppendLine(finding.Message);
            }

            return text.ToString();
        }

        _ = text.Append("pack ").Append(check.Id?.Value).Append(" passes: ").Append(check.Lenses).Append(" lenses, applies to ").AppendLine(check.Applies);
        _ = text.Append("hash ").AppendLine(check.Hash);
        _ = text.Append("pin line to approve: ").AppendLine(check.Pin);
        return text.ToString();
    }
}
