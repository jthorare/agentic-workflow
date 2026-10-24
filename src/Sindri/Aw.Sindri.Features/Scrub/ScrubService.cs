using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Features.Scrub;

/// <summary>'sindri scrub' (cutover spec §6.2): redacts a text through the text scrubber (Aw.Redaction), scans the added lines of the staged diff, or installs the pre-commit hook.</summary>
/// <param name="scrubber">Recognises and redacts secrets.</param>
/// <param name="git">The staged diff.</param>
/// <param name="installer">Writes the hook.</param>
internal sealed class ScrubService(ITextScrubber scrubber, IPreCommitGit git, IPreCommitInstaller installer) : IService<ScrubRequest, ScrubResponse>
{
    public async ValueTask<Result<ScrubResponse>> Handle(ScrubRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Mode switch
        {
            ScrubMode.Stdin => new ScrubResponse(scrubber.Scrub(request.Text)),
            ScrubMode.Staged => await Staged(request, cancellationToken).ConfigureAwait(false),
            ScrubMode.InstallPreCommit => await Install(request, cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(request)),
        };
    }

    private async ValueTask<Result<ScrubResponse>> Staged(ScrubRequest request, CancellationToken cancellationToken)
    {
        var added = await git.StagedAddedLines(request.Repo, cancellationToken).ConfigureAwait(false);
        if (added.IsError)
        {
            return added.Error;
        }

        var findings = SecretScan.Findings(SindriErrors.ScrubStagedSecrets.Code, added.Value, scrubber);
        if (findings.Count == 0)
        {
            return new ScrubResponse("No secrets in staged changes.\n");
        }

        var where = string.Join('\n', findings.Select(static f => string.Create(CultureInfo.InvariantCulture, $"{f.Path}:{f.Line}")));
        return SindriErrors.ScrubStagedSecrets.With($"refused: {findings.Count} likely secret(s) in staged changes (remove them and rotate them):\n{where}");
    }

    private async ValueTask<Result<ScrubResponse>> Install(ScrubRequest request, CancellationToken cancellationToken)
    {
        var bin = request.Bin ?? git.RunningBin;
        var done = await installer.Install(request.Repo, bin, cancellationToken).ConfigureAwait(false);
        return done.IsError ? done.Error : new ScrubResponse($"Installed the secret-scan pre-commit hook at {done.Value}.\n", done.Value);
    }
}
