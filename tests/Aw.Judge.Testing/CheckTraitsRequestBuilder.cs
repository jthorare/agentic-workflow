using Aw.Judge.Core.Providers;
using Aw.Judge.Features.CheckTraits;

namespace Aw.Judge.Testing;

/// <summary>Builds a CheckTraitsRequest with deterministic defaults: an edit of src/app.ts, every provider allowed.</summary>
public sealed class CheckTraitsRequestBuilder
{
    private CheckTraitsRequest _request = new(TraitCheckMode.Edit, ["src/app.ts"]);

    /// <summary>Checks the diff of these edited files.</summary>
    /// <param name="files">The files.</param>
    /// <returns>This builder.</returns>
    public CheckTraitsRequestBuilder Edit(params string[] files)
    {
        _request = new CheckTraitsRequest(TraitCheckMode.Edit, files);
        return this;
    }

    /// <summary>Checks the branch's diff against a ref.</summary>
    /// <param name="baseRef">The ref, or null for origin/main.</param>
    /// <returns>This builder.</returns>
    public CheckTraitsRequestBuilder Stop(string? baseRef = null)
    {
        _request = new CheckTraitsRequest(TraitCheckMode.Stop, [], baseRef);
        return this;
    }

    /// <summary>Checks whole files.</summary>
    /// <param name="paths">The paths; none for the whole repo.</param>
    /// <returns>This builder.</returns>
    public CheckTraitsRequestBuilder Scan(params string[] paths)
    {
        _request = new CheckTraitsRequest(TraitCheckMode.Scan, [], Paths: paths);
        return this;
    }

    /// <summary>Limits the providers (the --providers allowlist).</summary>
    /// <param name="providers">The allowed providers.</param>
    /// <returns>This builder.</returns>
    public CheckTraitsRequestBuilder Allowing(params ProviderName[] providers)
    {
        _request = _request with { Providers = providers };
        return this;
    }

    /// <summary>Runs the check for an agent session (--session).</summary>
    /// <param name="sessionId">The session's id.</param>
    /// <returns>This builder.</returns>
    public CheckTraitsRequestBuilder InSession(string sessionId)
    {
        _request = _request with { SessionId = Core.Ids.SessionId.From(sessionId) };
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public CheckTraitsRequest Build() => _request;
}
