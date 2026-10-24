using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Features.Scrub;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri scrub [--staged | --install-pre-commit [--repo PATH] [--bin PATH]]' (cutover spec §6.2): redact standard input, scan the
/// staged diff (exit 1 with SND-SCRUB-002 on a finding, which is what makes the pre-commit hook refuse the commit), or install the
/// hook. Local: it needs no control plane.
/// </summary>
/// <param name="services">The CLI's scoped services.</param>
/// <param name="output">Where answers go.</param>
/// <param name="stdin">Standard input.</param>
internal sealed class ScrubCommand(IServiceProvider services, Output output, TextReader stdin)
{
    internal const string Usage = "scrub [--staged | --install-pre-commit [--repo PATH] [--bin PATH]]";

    internal async Task<int> Run(string[] args, CancellationToken ct)
    {
        if (Parse(args) is not { } mode)
        {
            return await output.Error(SindriErrors.UnknownVerb.With($"'scrub {string.Join(' ', args)}' is not a scrub command: {Usage}")).ConfigureAwait(false);
        }

        var request = mode.Mode == ScrubMode.Stdin ? mode with { Text = await stdin.ReadToEndAsync(ct).ConfigureAwait(false) } : mode;
        var done = await services.GetRequiredService<IService<ScrubRequest, ScrubResponse>>().Handle(request, ct).ConfigureAwait(false);
        if (done.IsError)
        {
            var code = await output.Error(done.Error).ConfigureAwait(false);
            return done.Error.Code == SindriErrors.ScrubStagedSecrets.Code ? 1 : code;
        }

        return await output.Text(done.Value.Output, 0).ConfigureAwait(false);
    }

    private static ScrubRequest? Parse(string[] args) => args switch
    {
        [] => new ScrubRequest(ScrubMode.Stdin),
        ["--staged"] => new ScrubRequest(ScrubMode.Staged),
        ["--install-pre-commit", .. var rest] => Install(rest),
        _ => null,
    };

    // '--repo PATH' and '--bin PATH', each at most once, in either order.
    private static ScrubRequest? Install(string[] flags)
    {
        Dictionary<string, string> given = new(StringComparer.Ordinal);
        for (var i = 0; i < flags.Length; i += 2)
        {
            if (i + 1 >= flags.Length || flags[i] is not ("--repo" or "--bin") || flags[i + 1].StartsWith("--", StringComparison.Ordinal) || !given.TryAdd(flags[i], flags[i + 1]))
            {
                return null;
            }
        }

        return new ScrubRequest(ScrubMode.InstallPreCommit, Repo: given.GetValueOrDefault("--repo"), Bin: given.GetValueOrDefault("--bin"));
    }
}
