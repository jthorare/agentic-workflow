using Aw.Forge.Core.Pulls;
using Aw.Forge.Core.Refs;
using Aw.Kernel;

namespace Aw.Forge.Core.Ports;

/// <summary>
/// The forge port (PB10, CFG-13): recipes and blocks reach a code forge only through this. An adapter refuses a ref or repo
/// whose host isn't allowed with AW-FORGE-002 before it sends anything. Text it returns from the forge is untrusted.
/// </summary>
public interface IForge
{
    /// <summary>Which adapter this is.</summary>
    public ForgeKind Kind { get; }

    /// <summary>Parses a pull request link against the allowed hosts; sends nothing.</summary>
    /// <param name="link">The link.</param>
    /// <returns>The ref, or the refusal naming the part that failed.</returns>
    public Result<ForgeRef> Parse(string? link);

    /// <summary>Reads a pull request.</summary>
    /// <param name="pull">The pull request.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The snapshot.</returns>
    public ValueTask<Result<PullSnapshot>> GetPull(ForgeRef pull, CancellationToken cancellationToken);

    /// <summary>Reads the poller's cheap digest of several pull requests.</summary>
    /// <param name="pulls">The pull requests.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>One digest per pull request, in order.</returns>
    public ValueTask<Result<IReadOnlyList<PullDigest>>> GetPullDigest(IReadOnlyList<ForgeRef> pulls, CancellationToken cancellationToken);

    /// <summary>Lists threads, review bodies and top-level comments.</summary>
    /// <param name="pull">The pull request.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The conversation, oldest first.</returns>
    public ValueTask<Result<IReadOnlyList<ForgeThread>>> ListThreads(ForgeRef pull, CancellationToken cancellationToken);

    /// <summary>Reads a check's log, capped to ForgeLimits.LogExcerptCharacters and scrubbed.</summary>
    /// <param name="pull">The pull request.</param>
    /// <param name="check">The check's name.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The excerpt.</returns>
    public ValueTask<Result<string>> CheckLog(ForgeRef pull, string check, CancellationToken cancellationToken);

    /// <summary>Posts one reply, once per marker.</summary>
    /// <param name="request">The reply.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Whether this call posted it.</returns>
    public ValueTask<Result<Delivery>> Reply(ReplyRequest request, CancellationToken cancellationToken);

    /// <summary>Adds one reaction, once per marker.</summary>
    /// <param name="request">The reaction.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Whether this call added it.</returns>
    public ValueTask<Result<Delivery>> React(ReactionRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Pushes an explicit sha to the profile's URL with the push hardened: --no-verify, hooks off, and only explicit
    /// configuration (nothing from the repository's own config), with a lease on the branch's tip.
    /// </summary>
    /// <param name="request">The push.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Pushed or HeadMoved; any other failure is an error.</returns>
    public ValueTask<Result<PushOutcome>> Push(PushRequest request, CancellationToken cancellationToken);

    /// <summary>Finds an open pull request by head branch, author and marker.</summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The ref, or null when there is none.</returns>
    public ValueTask<Result<ForgeRef?>> FindPull(FindPullQuery query, CancellationToken cancellationToken);

    /// <summary>Opens a pull request.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The new pull request.</returns>
    public ValueTask<Result<ForgeRef>> OpenPull(OpenPullRequest request, CancellationToken cancellationToken);

    /// <summary>Changes a pull request's title or body.</summary>
    /// <param name="request">The change.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Success or the error.</returns>
    public ValueTask<Result<Unit>> UpdatePull(UpdatePullRequest request, CancellationToken cancellationToken);

    /// <summary>Points a pull request at another base branch.</summary>
    /// <param name="pull">The pull request.</param>
    /// <param name="newBase">The branch name.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Success or the error.</returns>
    public ValueTask<Result<Unit>> Retarget(ForgeRef pull, string newBase, CancellationToken cancellationToken);

    /// <summary>Merges only if the head is still the expected one.</summary>
    /// <param name="request">The merge.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Merged, Blocked (with the reason) or HeadMoved.</returns>
    public ValueTask<Result<MergeOutcome>> Merge(MergeRequest request, CancellationToken cancellationToken);

    /// <summary>Re-runs the failed checks of a commit.</summary>
    /// <param name="pull">The pull request.</param>
    /// <param name="sha">The commit.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>How many checks were re-run.</returns>
    public ValueTask<Result<int>> RerunChecks(ForgeRef pull, string sha, CancellationToken cancellationToken);

    /// <summary>The login the credential acts as, as the forge reports it (GitHub: GET /user, read once and kept).</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The login.</returns>
    public ValueTask<Result<string>> Self(CancellationToken cancellationToken);

    /// <summary>Reads a repository's default branch.</summary>
    /// <param name="repo">The repository.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The branch name.</returns>
    public ValueTask<Result<string>> DefaultBranch(ForgeRepo repo, CancellationToken cancellationToken);
}
