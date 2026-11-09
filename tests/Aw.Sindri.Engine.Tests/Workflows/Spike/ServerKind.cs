namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>Which pinned Temporal server a probe test runs against.</summary>
public enum ServerKind
{
    /// <summary>The Java SDK's time-skipping test server (temporal-test-server), via WorkflowEnvironment.StartTimeSkippingAsync.</summary>
    TimeSkipping,

    /// <summary>The temporal CLI's dev server, via WorkflowEnvironment.StartLocalAsync.</summary>
    Local,
}
