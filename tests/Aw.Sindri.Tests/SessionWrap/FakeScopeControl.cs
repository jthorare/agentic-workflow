using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Tests.SessionWrap;

internal sealed class FakeScopeControl(TimeProvider clock) : IScopeControl
{
    private readonly List<(DateTimeOffset At, string Signal)> _kills = [];
    private readonly List<(DateTimeOffset At, TimeSpan Ceiling)> _ceilings = [];

    /// <summary>What <see cref="ControlGroup"/> answers.</summary>
    public string? Group { get; set; } = "/user.slice/sindri-test.scope";

    /// <summary>What <see cref="Result"/> answers: systemd's 'Result' of the unit.</summary>
    public string? UnitResult { get; set; } = "success";

    public IReadOnlyList<(DateTimeOffset At, string Signal)> Kills { get { lock (_kills) { return [.. _kills]; } } }

    public IReadOnlyList<(DateTimeOffset At, TimeSpan Ceiling)> Ceilings { get { lock (_ceilings) { return [.. _ceilings]; } } }

    public Task<string?> ControlGroup(string unit, CancellationToken cancellationToken) => Task.FromResult(Group);

    public Task<string?> Result(string unit, CancellationToken cancellationToken) => Task.FromResult(UnitResult);

    public Task<bool> Kill(string unit, string signal, CancellationToken cancellationToken)
    {
        lock (_kills)
        {
            _kills.Add((clock.GetUtcNow(), signal));
        }

        return Task.FromResult(true);
    }

    public Task<bool> SetRuntimeMax(string unit, TimeSpan ceiling, CancellationToken cancellationToken)
    {
        lock (_ceilings)
        {
            _ceilings.Add((clock.GetUtcNow(), ceiling));
        }

        return Task.FromResult(true);
    }
}
