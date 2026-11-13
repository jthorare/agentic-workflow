namespace Aw.Sindri.Core.Graph.Runs;

/// <summary>The two modes of a workflow run (Workflows page spec §3.2): a live run does real work, a test run records what it would have done. The text is what the run row, the events and the API carry.</summary>
public static class WorkflowRunMode
{
    /// <summary>A live run: ports act for real.</summary>
    public const string Live = "live";

    /// <summary>A test run: every write port is a recorder.</summary>
    public const string Test = "test";
}
