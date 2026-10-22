using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Prove;
using Aw.Sindri.Infrastructure.Workflow;

namespace Aw.Sindri.Infrastructure.Prove;

/// <summary><see cref="ICommitText"/> through 'git log -p': the messages and patches of <c language="text">base..head</c>, read-only and bounded to 30 s. A commit that is not a hex sha is refused before git runs, so no argument is ever an option.</summary>
internal sealed class GitCommitText : ICommitText
{
    /// <inheritdoc/>
    public async ValueTask<Result<string>> Range(string checkout, string baseSha, string head, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(checkout);
        if (!IsSha(baseSha) || !IsSha(head))
        {
            return SindriErrors.InvalidInput.With("the commits to read must be hex shas");
        }

        var run = await GitProcess.Run(checkout, ["log", "-p", "--no-color", "--no-ext-diff", "--no-textconv", $"{baseSha}..{head}", "--"], cancellationToken).ConfigureAwait(false);
        return run.Exit == 0 ? run.Output : SindriErrors.InvalidInput.With($"git can't read {baseSha}..{head} in {checkout}: {run.Error.Trim()}");
    }

    private static bool IsSha(string? text) => !string.IsNullOrEmpty(text) && text.Length is >= 4 and <= 64 && text.All(static c => char.IsAsciiHexDigit(c));
}
