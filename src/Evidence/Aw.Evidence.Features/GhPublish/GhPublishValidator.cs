using Aw.Evidence.Core;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Evidence.Features.GhPublish;

/// <summary>The run directory is absolute, the repo is owner/name, the PR number is 1 or more.</summary>
internal sealed class GhPublishValidator : IValidator<GhPublishRequest>
{
    public Result<Unit> Validate(GhPublishRequest value)
    {
        if (!value.RunDirectory.StartsWith('/'))
        {
            return EvidenceErrors.Invalid.With("the run directory must be absolute");
        }

        if (!IsOwnerName(value.Repo))
        {
            return EvidenceErrors.Invalid.With("--repo must be owner/name");
        }

        return value.Pr >= 1 ? Unit.Value : EvidenceErrors.Invalid.With("--pr must be 1 or more");
    }

    private static bool IsOwnerName(string repo)
    {
        var parts = repo.Split('/');
        return parts.Length == 2 && parts.All(static p => p.Length > 0 && p != "." && p != ".." && p.All(static c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.'));
    }
}
