using Aw.Kernel;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Testing.Sandbox;

/// <summary>A test-only sandbox that records every call to an inner sandbox and can be told to fail one.</summary>
/// <remarks>Wraps <paramref name="inner"/> (default: a <see cref="PassThroughSandbox"/>).</remarks>
/// <param name="inner">The sandbox that answers.</param>
public sealed class RecordingSandbox(ISandbox? inner = null) : ISandbox
{
    private readonly ISandbox _inner = inner ?? new PassThroughSandbox();
    private readonly List<SandboxRequest> _requests = [];
    private readonly List<SandboxPlan> _checked = [];
    private readonly List<IReadOnlyList<string>> _wrapped = [];
    private Error? _selfCheckFailure;

    /// <summary>The requests <see cref="Plan"/> got.</summary>
    public IReadOnlyList<SandboxRequest> Requests => _requests;

    /// <summary>The plans <see cref="SelfCheck"/> got.</summary>
    public IReadOnlyList<SandboxPlan> Checked => _checked;

    /// <summary>The argv <see cref="Wrap"/> got.</summary>
    public IReadOnlyList<IReadOnlyList<string>> Wrapped => _wrapped;

    /// <summary>Makes the next <see cref="SelfCheck"/> fail with <paramref name="error"/>.</summary>
    /// <param name="error">The error.</param>
    public void FailSelfCheckWith(Error error) => _selfCheckFailure = error;

    /// <inheritdoc />
    public Result<SandboxCapabilities> Probe() => _inner.Probe();

    /// <inheritdoc />
    public Result<SandboxPlan> Plan(SandboxRequest request)
    {
        _requests.Add(request);
        return _inner.Plan(request);
    }

    /// <inheritdoc />
    public Task<Result<Unit>> SelfCheck(SandboxPlan plan, CancellationToken ct)
    {
        _checked.Add(plan);
        if (_selfCheckFailure is { } failure)
        {
            _selfCheckFailure = null;
            return Task.FromResult<Result<Unit>>(failure);
        }

        return _inner.SelfCheck(plan, ct);
    }

    /// <inheritdoc />
    public Result<IReadOnlyList<string>> Wrap(SandboxPlan plan, IReadOnlyList<string> argv)
    {
        _wrapped.Add(argv);
        return _inner.Wrap(plan, argv);
    }
}
