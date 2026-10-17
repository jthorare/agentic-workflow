using Aw.Sindri.Core.Launch;
using Xunit;

namespace Aw.Sindri.Testing.Launch;

/// <summary>The contract every <see cref="ITerminalViewer"/> honours, run against the fake and (U6) the warp, exec-template and print adapters.</summary>
public abstract class TerminalViewerContract
{
    /// <summary>The viewer under test.</summary>
    /// <returns>The viewer.</returns>
    protected abstract ITerminalViewer Create();

    /// <summary>A request the viewer under test can show.</summary>
    /// <returns>The request.</returns>
    protected abstract ViewerRequest Request();

    /// <summary>A viewer problem is an outcome, never an exception, and a failure names a launch code.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Open_answers_with_an_outcome_and_a_failure_names_a_launch_code()
    {
        var outcome = await Create().Open(Request(), CancellationToken.None);

        Expect.True(Enum.IsDefined(outcome.Kind));
        Expect.True(outcome.Kind == ViewerOutcomeKind.Failed
            ? outcome.Code is { } code && code.StartsWith("SND-LAUNCH-", StringComparison.Ordinal)
            : outcome.Code is null);
    }

    /// <summary>The probe's id is the viewer's kind (launcher L-4).</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_probe_names_the_viewer_by_its_kind()
    {
        var viewer = Create();

        var facts = await viewer.Probe(CancellationToken.None);

        Expect.Equal(viewer.Kind, facts.Kind);
    }
}
