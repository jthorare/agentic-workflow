namespace Aw.Sindri.Ui.Testing;

/// <summary>The fake's run of a saved revision once (#3691): POST /sindri/workflows/{slug}/runs with a revision. It answers <see cref="StartWorkflowRunAnswer"/>, the same route's answer.</summary>
internal sealed partial class FakeSindriApi
{
    private readonly List<(string Slug, int Revision)> _startedRevisionRuns = [];

    /// <summary>Every workflow and revision a run-once was started for, in order.</summary>
    public IReadOnlyList<(string Slug, int Revision)> StartedRevisionRuns { get { lock (_gate) { return [.. _startedRevisionRuns]; } } }
}
