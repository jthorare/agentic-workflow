using Aw.Kernel;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Profile;
using Results = Aw.Kernel.Results;

namespace Aw.Sindri.Host.Forge;

/// <summary>
/// The <see cref="IPullRequests"/> Sindri registers: each call reads the approved profile's 'workflow.forge' block and goes to the adapter it names
/// (<see cref="ForgePullRequests"/>), or to <see cref="NoForgePullRequests"/> when the block names none, so approving a profile switches the forge
/// without a restart. The adapter is built once per distinct block.
/// </summary>
/// <param name="approved">The approved profile.</param>
/// <param name="environment">Reads an environment variable.</param>
/// <param name="clock">The clock.</param>
internal sealed class ConfiguredPullRequests(IApprovedProfile approved, Func<string, string?> environment, TimeProvider clock) : IPullRequests
{
    private static readonly NoForgePullRequests _none = new();

    private readonly Lock _gate = new();
    private (string Key, IPullRequests Adapter)? _built;

    public ValueTask<Result<PullView>> Get(string link, CancellationToken cancellationToken)
        => Call(pulls => pulls.Get(link, cancellationToken), cancellationToken);

    public ValueTask<Result<string>> CheckLog(string link, string check, CancellationToken cancellationToken)
        => Call(pulls => pulls.CheckLog(link, check, cancellationToken), cancellationToken);

    public ValueTask<Result<PullPushOutcome>> Push(PullPush push, CancellationToken cancellationToken)
        => Call(pulls => pulls.Push(push, cancellationToken), cancellationToken);

    public ValueTask<Result<int>> RerunChecks(string link, string sha, CancellationToken cancellationToken)
        => Call(pulls => pulls.RerunChecks(link, sha, cancellationToken), cancellationToken);

    public ValueTask<Result<IReadOnlyList<PullThread>>> Threads(string link, CancellationToken cancellationToken)
        => Call(pulls => pulls.Threads(link, cancellationToken), cancellationToken);

    public ValueTask<Result<PullMergeOutcome>> Merge(PullMerge request, CancellationToken cancellationToken)
        => Call(pulls => pulls.Merge(request, cancellationToken), cancellationToken);

    public ValueTask<Result<PullDelivery>> Reply(PullReply request, CancellationToken cancellationToken)
        => Call(pulls => pulls.Reply(request, cancellationToken), cancellationToken);

    public ValueTask<Result<PullDelivery>> React(PullReaction request, CancellationToken cancellationToken)
        => Call(pulls => pulls.React(request, cancellationToken), cancellationToken);

    public ValueTask<Result<PullPushOutcome>> PushBranch(PullBranchPush push, CancellationToken cancellationToken)
        => Call(pulls => pulls.PushBranch(push, cancellationToken), cancellationToken);

    public ValueTask<Result<OpenedPull?>> Find(FindPull query, CancellationToken cancellationToken)
        => Call(pulls => pulls.Find(query, cancellationToken), cancellationToken);

    public ValueTask<Result<OpenedPull>> Open(OpenPull request, CancellationToken cancellationToken)
        => Call(pulls => pulls.Open(request, cancellationToken), cancellationToken);

    public ValueTask<Result<Unit>> Update(string link, string? title, string? body, CancellationToken cancellationToken)
        => Call(pulls => pulls.Update(link, title, body, cancellationToken), cancellationToken);

    private async ValueTask<Result<T>> Call<T>(Func<IPullRequests, ValueTask<Result<T>>> call, CancellationToken cancellationToken)
    {
        var pulls = await Resolve(cancellationToken).ConfigureAwait(false);
        return pulls.IsError ? pulls.Error : await call(pulls.Value).ConfigureAwait(false);
    }

    private async ValueTask<Result<IPullRequests>> Resolve(CancellationToken cancellationToken)
    {
        var latest = await approved.Latest(cancellationToken).ConfigureAwait(false);
        if (latest.IsError)
        {
            return latest.Error;
        }

        if (latest.Value?.Profile.Workflow?.Forge is not { Adapter: not null } settings)
        {
            return _none;
        }

        var key = string.Join('\n', settings.Adapter, string.Join(',', settings.Hosts ?? []), settings.Credential);
        lock (_gate)
        {
            if (_built is { } built && built.Key == key)
            {
                return Results.Success(built.Adapter);
            }
        }

        var forge = ForgeAdapters.Create(settings, environment, clock);
        if (forge.IsError)
        {
            return forge.Error;
        }

        IPullRequests adapter = new ForgePullRequests(forge.Value);
        lock (_gate)
        {
            _built = (key, adapter);
        }

        return Results.Success(adapter);
    }
}
