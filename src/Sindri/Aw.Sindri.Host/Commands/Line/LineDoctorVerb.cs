using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Features.LineDoctor;

namespace Aw.Sindri.Host.Commands.Line;

/// <summary>
/// 'sindri line doctor [&lt;repo&gt;] [--json]' (line spec Appendix D, LN-API4, class local): whether a repo is set up for the line. Reads the approved profile, the checkout and the box's cap in this process, writes
/// nothing and never starts the control plane. Exit 0 when every check is ok, 1 when one warns or fails, 2 on a usage or tool error.
/// </summary>
internal static class LineDoctorVerb
{
    private const string Usage = "usage: sindri line doctor [<repo>] [--json]";

    internal static async Task<int> Run(IReadOnlyList<string> args, IServiceProvider services, Output output, CancellationToken ct)
    {
        var json = args.Contains("--json", StringComparer.Ordinal);
        var paths = args.Where(static a => a != "--json").ToList();
        if (paths.Count > 1 || paths.Exists(static p => p.StartsWith('-')))
        {
            return await LineCommandTable.Fail(output, SindriErrors.InvalidInput.With(Usage)).ConfigureAwait(false);
        }

        var directory = Path.GetFullPath(paths.Count == 1 ? paths[0] : Environment.CurrentDirectory);
        var result = await services.GetRequiredService<IService<LineDoctorRequest, LineDoctorResponse>>().Handle(new LineDoctorRequest(directory), ct).ConfigureAwait(false);
        if (result.IsError)
        {
            return await LineCommandTable.Fail(output, result.Error).ConfigureAwait(false);
        }

        var code = result.Value.Passed ? 0 : 1;
        return json ? await output.Json(result.Value, code).ConfigureAwait(false) : await output.Text(Text(result.Value), code).ConfigureAwait(false);
    }

    /// <summary>The report as text: one line per check, as 'sindri doctor' prints them.</summary>
    /// <param name="report">The report.</param>
    /// <returns>The text.</returns>
    internal static string Text(LineDoctorResponse report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var lines = report.Checks.Select(static c => $"{Word(c.State),-5} {c.Name,-24} {c.Detail}{(c.Fix is null ? string.Empty : $" (fix: {c.Fix})")}");
        return string.Join('\n', lines) + "\n";
    }

    private static string Word(CheckState state) => state switch
    {
        CheckState.Ok => "ok",
        CheckState.Warn => "warn",
        CheckState.Fail => "fail",
        _ => "?",
    };
}
