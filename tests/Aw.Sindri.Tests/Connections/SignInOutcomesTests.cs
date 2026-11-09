using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Tests.Connections;

/// <summary>Only the latest sign-in try of a connection name records or clears its outcome (issue #2420).</summary>
[Lane(TestLane.Unit)]
public sealed class SignInOutcomesTests
{
    private const string Name = "linear";

    [Fact]
    public void An_older_try_that_fails_after_a_newer_one_began_is_ignored()
    {
        var outcomes = new SignInOutcomes();
        var older = outcomes.Begin(Name);
        _ = outcomes.Begin(Name);

        outcomes.Failed(older, SindriErrors.ConnectionSignInTimeout.With("too late"), KnownInstants.Epoch);

        Assert.Null(outcomes.LastFailure(Name));
    }

    [Fact]
    public void The_latest_try_that_fails_is_kept()
    {
        var outcomes = new SignInOutcomes();
        _ = outcomes.Begin(Name);
        var latest = outcomes.Begin(Name);
        var error = SindriErrors.ConnectionConsentDenied.With("denied");

        outcomes.Failed(latest, error, KnownInstants.Epoch);

        Assert.Equal(error.Code, outcomes.LastFailure(Name)!.Error.Code);
    }

    [Fact]
    public void An_older_try_that_succeeds_does_not_clear_the_latest_tries_failure()
    {
        var outcomes = new SignInOutcomes();
        var older = outcomes.Begin(Name);
        var latest = outcomes.Begin(Name);
        var error = SindriErrors.ConnectionConsentDenied.With("denied");
        outcomes.Failed(latest, error, KnownInstants.Epoch);

        outcomes.Clear(older);

        Assert.Equal(error.Code, outcomes.LastFailure(Name)!.Error.Code);
    }
}
